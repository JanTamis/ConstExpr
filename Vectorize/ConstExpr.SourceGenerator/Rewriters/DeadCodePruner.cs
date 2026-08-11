using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Simplified dead code pruner using the Mark-and-Sweep pattern.
///   First collects all variable usages, then prunes in a single rewrite pass.
/// </summary>
public sealed class DeadCodePruner(VariableUsageCollector usageCollector, IDictionary<string, VariableItem> variables, SemanticModel model, ISet<string> locallyDeclaredVariables, bool isFullScope) : CSharpSyntaxRewriter
{
	/// <summary>
	///   Prunes dead code from a syntax node using the Mark-and-Sweep pattern.
	///   <paramref name="isFullScope" /> is whether <paramref name="node" /> is the complete body of
	///   the function/lambda that <paramref name="variables" />'s entries belong to (every
	///   declaration and every read of every TRACKED variable — e.g. a parameter, which never has a
	///   <c>var</c> declarator to be found inside any sub-tree — is guaranteed to be inside
	///   <paramref name="node" />). Pass <see langword="false" /> when pruning a narrower sub-tree
	///   (e.g. a single block's tail) carved out of a larger body: a variable declared in an
	///   ancestor scope can have real reads outside that sub-tree — after a loop it sits in, for
	///   instance — that this narrower view never sees, so the <see cref="CanBePrunedAssignment" />
	///   literal-RHS fallback must not apply to it there. A variable whose own <c>var</c> declarator
	///   IS found inside <paramref name="node" /> is safe regardless, since nothing outside its own
	///   declaration's scope could possibly read it — see <see cref="CanBePrunedAssignment" />.
	/// </summary>
	public static SyntaxNode Prune(SyntaxNode node, IDictionary<string, VariableItem> variables, SemanticModel model, bool isFullScope = true)
	{
		// Phase 1: Mark - collect all variable usages
		// Include both tracked variables and any local variable declarators found in the node
		// (some locals are introduced during rewriting and are not present in the variables dictionary).
		var declaredLocals = new HashSet<string>(node.DescendantNodes().OfType<VariableDeclaratorSyntax>()
			.Select(v => v.Identifier.Text));

		var allTracked = variables.Keys.Concat(declaredLocals).Distinct();

		var collector = new VariableUsageCollector(allTracked);
		collector.Visit(node);

		// Phase 2: Sweep - rewrite and prune dead code
		var pruner = new DeadCodePruner(collector, variables, model, declaredLocals, isFullScope);
		var result = pruner.Visit(node);

		// When all statements in a top-level block are pruned, the visitor returns null.
		// Return an empty block so callers always receive a valid SyntaxNode.
		if (result is null && node is BlockSyntax emptyBlock)
		{
			return emptyBlock.WithStatements(List<StatementSyntax>());
		}

		return result ?? node;
	}

	/// <summary>
	///   Determines if a variable can be pruned based on collected usage data and variable state.
	///   Used for assignments and other non-declaration contexts; untracked variables are kept.
	/// </summary>
	private bool CanBePruned(string variableName)
	{
		// Must not be read anywhere
		if (!usageCollector.CanBePruned(variableName))
		{
			return false;
		}

		// If variable is not tracked, keep it (we don't know enough about it to prune safely)
		if (!variables.TryGetValue(variableName, out var variable))
		{
			return false;
		}

		// For tracked variables, must have a constant value and not be altered
		return variable.HasValue && !variable.IsAltered;
	}

	/// <summary>
	///   Determines if an assignment expression to a variable can be pruned.
	///   In addition to the standard <see cref="CanBePruned(string)" /> check, this also
	///   handles the case where <see cref="VariableItem.HasValue" /> was cleared by
	///   <c>InvalidateAssignedVariables</c> (after an if/else branch) even though the
	///   actual RHS is a side-effect-free constant literal. A dead write with no side
	///   effects is always safe to remove.
	/// </summary>
	private bool CanBePrunedAssignment(string variableName, ExpressionSyntax rhs)
	{
		if (!usageCollector.CanBePruned(variableName))
		{
			return false;
		}

		if (!variables.TryGetValue(variableName, out var variable))
		{
			return false;
		}

		// Standard path: the variable still carries its known constant value.
		if (variable.HasValue && !variable.IsAltered)
		{
			return true;
		}

		// Fallback: HasValue may have been cleared by InvalidateAssignedVariables after an
		// if/else with an unknown condition, even though the rewritten RHS is a literal.
		// Pruning a dead literal write is always safe regardless of HasValue — but only when the
		// pruned tree provably contains the variable's whole lifetime, so "not read anywhere in
		// `node`" really does mean "not read anywhere". That holds when either:
		//  - the variable's own `var` declarator is inside `node` (nothing outside its declaring
		//    scope could read it, e.g. a branch-local temp like a switch's `i`/`f`/`p`/`q`/`t`), or
		//  - `isFullScope` says `node` already IS the whole enclosing function/lambda body, which
		//    covers variables with no declarator inside `node` at all — a parameter, or a local
		//    declared before the sub-block a caller (e.g. HoistCommonBranchAssignments) narrowed
		//    `node` down to. Without either, a caller running this pruner over a narrower sub-tree
		//    (e.g. just a loop body) would see zero reads simply because the variable's real read
		//    sits outside that sub-tree (after the loop, in an ancestor scope) — not because the
		//    write is genuinely dead.
		return (locallyDeclaredVariables.Contains(variableName) || isFullScope) && IsConstantExpression(rhs);
	}

	/// <summary>
	///   Determines if a variable declaration can be pruned. Unlike <see cref="CanBePruned(string)" />,
	///   this overload also handles variables that are not in the tracking dictionary — locals
	///   introduced during rewriting (a CSE temp, a hoisted invariant) that the interpreter never
	///   modelled — by checking that dropping the initializer cannot lose a side effect.
	///   This covers block-local variables introduced inside if/else branches whose scope does not
	///   extend beyond the branch.
	/// </summary>
	private bool CanBePrunedDeclaration(string variableName, ExpressionSyntax? initializer)
	{
		if (!usageCollector.CanBePruned(variableName))
		{
			return false;
		}

		if (!variables.TryGetValue(variableName, out var variable))
		{
			// An untracked variable that is still written somewhere must keep its declaration:
			// CanBePrunedAssignment refuses to prune a write to an untracked variable, so dropping
			// the declaration here would strand `x = …;` with nothing declaring it (CS0103).
			if (usageCollector.GetWriteCount(variableName) > 0)
			{
				return false;
			}

			// The doc contract for this overload: an untracked declaration only goes away when its
			// initializer cannot have a side effect. Without this the pruner silently deletes the
			// call in `var unused = Foo();`.
			return HasNoSideEffects(initializer);
		}

		// IsAltered is intentionally not checked when there are no other writes: if the
		// variable is never read, a single dead initializer is still dead code regardless of
		// staleness. But when another statement still assigns to this variable (e.g. `b` inside
		// a chained assignment `r = g = b = expr`), that write is judged by CanBePrunedAssignment,
		// which DOES require `!IsAltered`. If IsAltered is true, that assignment survives pruning —
		// so the declaration must survive too, or the surviving assignment references an undeclared
		// variable (CS0103).
		return variable.HasValue && (!variable.IsAltered || usageCollector.GetWriteCount(variableName) == 0);
	}

	/// <summary>
	///   Returns <see langword="true" /> when evaluating <paramref name="expr" /> cannot be observed —
	///   so discarding it along with its declaration changes nothing. Deliberately broader than
	///   <see cref="IsConstantExpression" />, which only admits literals: a synthesized temp's
	///   initializer is usually plain arithmetic over locals, and refusing to prune those would leave
	///   the dead CSE/hoist temps this pass exists to remove. Anything that can call code, allocate,
	///   assign, or mutate is rejected.
	/// </summary>
	private static bool HasNoSideEffects(ExpressionSyntax? expr)
	{
		if (expr is null)
		{
			return true;
		}

		foreach (var node in expr.DescendantNodesAndSelf())
		{
			switch (node)
			{
				case InvocationExpressionSyntax:
				case ObjectCreationExpressionSyntax:
				case ImplicitObjectCreationExpressionSyntax:
				case ArrayCreationExpressionSyntax:
				case ImplicitArrayCreationExpressionSyntax:
				case StackAllocArrayCreationExpressionSyntax:
				case ImplicitStackAllocArrayCreationExpressionSyntax:
				case AssignmentExpressionSyntax:
				case AwaitExpressionSyntax:
				case PostfixUnaryExpressionSyntax:
					return false;
				case PrefixUnaryExpressionSyntax prefix
					when prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression):
					return false;
			}
		}

		return true;
	}

	/// <summary>
	///   Returns <see langword="true" /> when the expression is guaranteed to be a side-effect-free
	///   constant (literal, default, or a unary minus applied to a literal).
	/// </summary>
	private static bool IsConstantExpression(ExpressionSyntax? expr)
	{
		return expr switch
		{
			null => true,
			LiteralExpressionSyntax => true,
			DefaultExpressionSyntax => true,
			PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax } => true,
			_ => false
		};
	}

	public override SyntaxNode? Visit(SyntaxNode? node)
	{
		try
		{
			return base.Visit(node);
		}
		catch (Exception)
		{
			return null;
		}
	}

	#region Statement Pruning

	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		if (Visit(node.Declaration) is not VariableDeclarationSyntax declaration || declaration.Variables.Count == 0)
		{
			return null;
		}

		return node.WithDeclaration(declaration);
	}

	public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
	{
		var remainingVariables = node.Variables
			.Where(v => !CanBePrunedDeclaration(v.Identifier.Text, v.Initializer?.Value))
			.ToList();

		switch (remainingVariables.Count)
		{
			case 0:
			{
				return null;
			}
			case 1:
			{
				// `var x = stackalloc T[n]` infers a pointer (`T*`) in an unsafe-enabled compilation,
				// not `Span<T>` — so normalizing a stackalloc declaration to `var` would change its
				// type. Keep the explicit `Span<T>` the StackAllocRewriter emitted. An uninitialized
				// declarator (initializer already elided) must also keep its explicit type: `var x;`
				// with no initializer is CS0818.
				//
				// A `ref` local is the same class of problem, and it is why pass 13 in
				// OptimizationPipeline runs without a Prune: the `ref` lives in `node.Type` as a
				// RefTypeSyntax (`ref var`), so replacing the type with a bare `var` deletes it and
				// turns `ref var r = ref x;` into `var r = ref x;` — CS8172, with no diagnostic from
				// this pass. BoundsCheckRewriter.ReferenceDeclaration emits exactly that shape, and so
				// does any user source that declares a ref local inside a [ConstExpr] method.
				//
				// Span<T>/ReadOnlySpan<T> is the same problem again: `Span<int> buf = array;` goes
				// through an implicit reference conversion, so `var` would infer `array`'s own type -
				// and once the initializer folds to a literal collection, `var buf = [1, 2, 3];` does
				// not even compile. See ConstExprPartialRewriter.SimplifiedTypeOf, which needed the
				// identical guard.
				if (node.Type is not RefTypeSyntax
				    && !IsSpanType(node.Type)
				    && remainingVariables[0].Initializer?.Value is not null and not (StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax))
				{
					node = node.WithType(ParseTypeName("var"));
				}

				break;
			}
		}

		return node.WithVariables(SeparatedList(remainingVariables));
	}

	// Ponytail: matches the type name only, not the namespace - the same trade-off BoundsCheckRewriter's
	// own Classify makes, since a user type of the same name would fail loudly as a compile error rather
	// than silently misbehave. Mirrors ConstExprPartialRewriter.IsSpanType.
	private static bool IsSpanType(TypeSyntax type)
	{
		return type switch
		{
			GenericNameSyntax { Identifier.Text: "Span" or "ReadOnlySpan", TypeArgumentList.Arguments.Count: 1 } => true,
			QualifiedNameSyntax qualified => IsSpanType(qualified.Right),
			_ => false
		};
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		switch (node.Expression)
		{
			// Prune assignments to dead variables
			case AssignmentExpressionSyntax assignment when ShouldPruneAssignment(assignment):
			// Prune increment/decrement on dead variables
			case PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax postfixId }
				when CanBePruned(postfixId.Identifier.Text):
			case PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax prefixId }
				when CanBePruned(prefixId.Identifier.Text):
			// Prune bare literal expression statements (e.g. a fully-resolved array-element
			// assignment/increment left behind as a placeholder) — no side effect to preserve.
			case LiteralExpressionSyntax:
			// Same, but for a negative/positive literal — those render as a unary +/- wrapping a
			// LiteralExpressionSyntax (e.g. `-212`), not a bare LiteralExpressionSyntax.
			case PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax } unary
				when unary.IsKind(SyntaxKind.UnaryMinusExpression) || unary.IsKind(SyntaxKind.UnaryPlusExpression):
			{
				return null;
			}
			// A mutating call (`outliers.Add(2);`) on a receiver that is itself dead - a local whose
			// entire lifetime (including this call) is visible in this scope, with no remaining reads
			// - is prunable exactly like a dead assignment to that receiver would be. Only when every
			// argument is side-effect-free: an argument that isn't (e.g. a call still standing there)
			// would lose that side effect along with the statement. Does not apply to a mutating call
			// on a parameter - CanBePruned already refuses those via IsAltered/HasValue, since a
			// parameter's receiver can be aliased by the caller and the mutation must reach it.
			case InvocationExpressionSyntax
			{
				Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax mutatingCallReceiver },
				ArgumentList.Arguments: var mutatingCallArguments
			} when CanBePruned(mutatingCallReceiver.Identifier.Text) && mutatingCallArguments.All(a => IsConstantExpression(a.Expression)):
			{
				return null;
			}
			default:
			{
				var visited = Visit(node.Expression);

				return visited is ExpressionSyntax expr ? node.WithExpression(expr) : node;
			}
		}
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		var statements = new List<StatementSyntax>();
		var terminalReached = false;

		foreach (var statement in node.Statements)
		{
			var visited = Visit(statement);

			switch (terminalReached)
			{
				// A bare empty statement (`;`) left behind by folding a statement down to a no-op
				// (e.g. `a ??= b;` when `a` is provably non-null - see ConstExprPartialRewriter's
				// VisitExpressionStatement) carries no meaning on its own and is safe to drop
				// unconditionally. Doesn't touch LabeledStatementSyntax wrapping an EmptyStatementSyntax
				// (goto continue-labels use that shape deliberately) - that's a different node type.
				case false when visited is EmptyStatementSyntax:
				{
					break;
				}
				case false when visited is StatementSyntax stmt:
				{
					statements.Add(stmt);

					if (IsTerminalStatement(stmt))
					{
						terminalReached = true;
					}
					break;
				}
				case true when visited is LocalFunctionStatementSyntax localFunc:
				{
					// Keep local functions even after terminal statements
					statements.Add(localFunc);
					break;
				}
			}
		}

		if (statements.Count == 0)
		{
			return null;
		}

		return node.WithStatements(List(statements));
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var statement = Visit(node.Statement);
		var elseClause = node.Else is not null ? Visit(node.Else) as ElseClauseSyntax : null;

		// If the body is empty and there's no else, remove the entire if
		if (statement is null or BlockSyntax { Statements.Count: 0 })
		{
			// Just the else remains - return its body
			return elseClause?.Statement;
		}

		var result = node.WithStatement(statement as StatementSyntax ?? node.Statement);

		if (elseClause is not null)
		{
			result = result.WithElse(elseClause);
		}
		else if (node.Else is not null)
		{
			result = result.WithElse(null);
		}

		return result;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		// If iterating over a pruned variable, remove the foreach
		if (node.Expression is IdentifierNameSyntax id && CanBePruned(id.Identifier.Text))
		{
			return null;
		}

		return base.VisitForEachStatement(node);
	}

	#endregion

	#region Expression Pruning

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		if (ShouldPruneAssignment(node))
		{
			return null;
		}

		var right = Visit(node.Right);

		if (right is null)
		{
			return null;
		}

		return node.WithRight(right as ExpressionSyntax ?? node.Right);
	}

	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		// If calling a method on a pruned variable, prune the call
		if (node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax id }
		    && CanBePruned(id.Identifier.Text)
		    && model.Compilation.GetTypeByMetadataName($"System.{id}") is null)
		{
			return null;
		}

		return base.VisitInvocationExpression(node);
	}

	#endregion

	#region Helpers

	private bool ShouldPruneAssignment(AssignmentExpressionSyntax assignment)
	{
		// Self-assignment: x = x — only applies to simple assignment (=), not compound ops like x *= x
		if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
		    && SyntaxNodeComparer.Get().Equals(assignment.Left, assignment.Right))
		{
			return true;
		}

		switch (assignment.Left)
		{
			// Assignment to prunable variable
			case IdentifierNameSyntax id when CanBePrunedAssignment(id.Identifier.Text, assignment.Right):
			{
				return true;
			}
			// Tuple assignment where all elements are prunable
			case TupleExpressionSyntax tuple:
			{
				var allPrunable = tuple.Arguments.All(arg =>
					arg.Expression is IdentifierNameSyntax tupleId && CanBePruned(tupleId.Identifier.Text));

				if (allPrunable)
				{
					return true;
				}
				break;
			}
		}

		return false;
	}

	private static bool IsTerminalStatement(StatementSyntax statement)
	{
		return statement is ReturnStatementSyntax
			or ThrowStatementSyntax
			or BreakStatementSyntax
			or ContinueStatementSyntax
			or YieldStatementSyntax { RawKind: (int) SyntaxKind.YieldBreakStatement };
	}

	#endregion

	#region Trivia Cleanup

	public override SyntaxToken VisitToken(SyntaxToken token)
	{
		if (token.IsKind(SyntaxKind.None))
		{
			return token;
		}

		var leading = FilterCommentTrivia(token.LeadingTrivia);
		var trailing = FilterCommentTrivia(token.TrailingTrivia);

		// Ensure space after 'return' keyword
		if (token.IsKind(SyntaxKind.ReturnKeyword) && token.Parent is ReturnStatementSyntax { Expression: not null })
		{
			if (!trailing.Any(t => t.IsKind(SyntaxKind.WhitespaceTrivia, SyntaxKind.EndOfLineTrivia)))
			{
				trailing = trailing.Add(Space);
			}
		}

		if (leading != token.LeadingTrivia || trailing != token.TrailingTrivia)
		{
			return token.WithLeadingTrivia(leading).WithTrailingTrivia(trailing);
		}

		return token;
	}

	private static SyntaxTriviaList FilterCommentTrivia(SyntaxTriviaList triviaList)
	{
		var filtered = triviaList.Where(t => t.Kind() switch
		{
			SyntaxKind.SingleLineCommentTrivia => false,
			SyntaxKind.MultiLineCommentTrivia => false,
			SyntaxKind.SingleLineDocumentationCommentTrivia => false,
			SyntaxKind.MultiLineDocumentationCommentTrivia => false,
			_ => true
		});

		return TriviaList(filtered);
	}

	#endregion
}