using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.ConditionalOptimizers;
using ConstExpr.SourceGenerator.Refactorers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Statement visitor methods for the ConstExprPartialRewriter.
///   Handles if, for, foreach, while, switch, return, block, and local declaration statements.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			if (value is true)
			{
				return Visit(node.Statement);
			}

			return node.Else is not null
				? Visit(node.Else.Statement)
				: null;
		}

		// Save variable state before visiting either branch. The condition is unknown so we
		// cannot tell which branch will execute at runtime. Each branch must be visited with
		// the pre-if state, and afterwards any variable written in either branch must be
		// marked as unknown so downstream code doesn't rely on a specific branch's value.
		var savedState = SaveVariableState();
		var statement = Visit(node.Statement);

		// Restore to pre-if state before visiting the else branch so it doesn't pick up
		// mutations that were made inside the then branch (e.g., h = 0 leaking into else).
		RestoreVariableState(savedState);

		var @else = Visit(node.Else);

		// After both branches: restore original state and invalidate every variable that
		// could have been written in either branch.
		RestoreVariableState(savedState);
		InvalidateAssignedVariables(node.Statement);

		if (node.Else is not null)
		{
			InvalidateAssignedVariables(node.Else.Statement);
		}

		if (@else is null)
		{
			switch (statement)
			{
				case IfStatementSyntax { Else: null } nestedIf:
				{
					condition = Visit(ParenthesizedExpression(LogicalOrExpression(condition as ExpressionSyntax ?? node.Condition, nestedIf.Condition)));
					statement = nestedIf.Statement;

					break;
				}
				case BlockSyntax { Statements: [ IfStatementSyntax { Else: null } nestedBlockIf ] }:
				{
					condition = Visit(ParenthesizedExpression(LogicalOrExpression(condition as ExpressionSyntax ?? node.Condition, nestedBlockIf.Condition)));
					statement = nestedBlockIf.Statement;

					break;
				}
			}
		}

		var result = node
			.WithCondition(condition as ExpressionSyntax ?? node.Condition)
			.WithStatement(statement as StatementSyntax ?? node.Statement)
			.WithElse(@else as ElseClauseSyntax);

		// Try to convert an if-else-if chain to a switch statement / switch expression.
		// Only attempt this when there is at least one else branch (otherwise a single-case
		// switch would be noisier than the original if).
		if (ConvertIfToSwitchCodeRefactoring.TryConvertIfElseChainToSwitch(result, out var switchNode))
		{
			return switchNode;
		}

		// Collapse an if/else whose branches both assign to the same variable (or both return a
		// value) into a single conditional expression. Each branch may be a bare statement or a
		// single-statement block, and either branch may itself already have been rewritten from a
		// nested if/else-if chain (e.g. an inner chain that collapsed into a bare assignment).
		// Because VisitIfStatement runs bottom-up, unwrapping the two branch shapes independently
		// lets a multi-level chain fold into one nested conditional expression instead of only the
		// innermost layer.
		if (@else is ElseClauseSyntax elseClause)
		{
			if (TryGetSingleAssignmentToIdentifier(statement, out var thenAssignment, out var thenTarget)
			    && TryGetSingleAssignmentToIdentifier(elseClause.Statement, out var elseAssignment, out var elseTarget)
			    && thenTarget == elseTarget)
			{
				return ExpressionStatement(
					VisitIfElseAssignment(condition, node.Condition, thenAssignment, elseAssignment));
			}

			if (TryGetSingleReturnExpression(statement, out var thenReturn)
			    && TryGetSingleReturnExpression(elseClause.Statement, out var elseReturn))
			{
				return ReturnStatement(
					Visit(ConditionalExpression(
						condition as ExpressionSyntax ?? node.Condition,
						thenReturn,
						elseReturn)) as ExpressionSyntax);
			}
		}

		return result;
	}

	/// <summary>
	///   Converts an if-else where both branches perform a simple assignment to the same variable
	///   into a single assignment. When the condition is a comparison and the operands match the
	///   assigned values (e.g. <c>if (a &gt; b) x = a; else x = b;</c>), the optimizer rewrites the
	///   right-hand side to a call like <c>T.MaxNative(a, b)</c> for any numeric type T. Falls back
	///   to an ordinary conditional expression assignment otherwise.
	/// </summary>
	private ExpressionSyntax VisitIfElseAssignment(
		SyntaxNode condition,
		ExpressionSyntax originalCondition,
		AssignmentExpressionSyntax thenAssignment,
		AssignmentExpressionSyntax elseAssignment)
	{
		var condExpr = ConditionalExpression(
			condition as ExpressionSyntax ?? originalCondition,
			thenAssignment.Right,
			elseAssignment.Right);

		// Resolve the element type from the branch operands — the synthetic conditional itself is
		// not present in the semantic model, but the original source identifiers/expressions are.
		if (!semanticModel.TryGetTypeSymbol(thenAssignment.Right, symbolStore, out var assignType))
		{
			semanticModel.TryGetTypeSymbol(elseAssignment.Right, symbolStore, out assignType);
		}

		if (assignType is not null)
		{
			var optimizer = new ConditionalExpressionOptimizer
			{
				Condition = condExpr.Condition,
				WhenTrue = condExpr.WhenTrue,
				WhenFalse = condExpr.WhenFalse,
				Type = assignType
			};

			if (optimizer.TryOptimize(loader, variables, out var optimizedRhs))
			{
				var visitedRhs = Visit(optimizedRhs) as ExpressionSyntax ?? optimizedRhs as ExpressionSyntax ?? condExpr;
				return thenAssignment.WithRight(visitedRhs);
			}
		}

		return Visit(thenAssignment.WithRight(condExpr)) as ExpressionSyntax
		       ?? thenAssignment.WithRight(condExpr);
	}

	/// <summary>
	///   Extracts a simple assignment to a named identifier from a branch that is either a bare
	///   <c>x = …;</c> expression statement or a single-statement block <c>{ x = …; }</c>. Returns
	///   <see langword="false" /> for any other shape. Handling both shapes lets an if/else fold into
	///   a conditional expression regardless of whether each branch happens to be braced.
	/// </summary>
	private static bool TryGetSingleAssignmentToIdentifier(
		SyntaxNode? statement,
		out AssignmentExpressionSyntax assignment,
		out string identifier)
	{
		assignment = null!;
		identifier = null!;

		var expression = statement switch
		{
			ExpressionStatementSyntax exprStmt => exprStmt.Expression,
			BlockSyntax { Statements: [ ExpressionStatementSyntax exprStmt ] } => exprStmt.Expression,
			_ => null
		};

		if (expression is AssignmentExpressionSyntax { Left: IdentifierNameSyntax id } simpleAssignment
		    && simpleAssignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
		{
			assignment = simpleAssignment;
			identifier = id.Identifier.Text;
			return true;
		}

		return false;
	}

	/// <summary>
	///   Extracts the returned expression from a branch that is either a bare <c>return …;</c> or a
	///   single-statement block <c>{ return …; }</c>. Returns <see langword="false" /> for a bare
	///   <c>return;</c> (no value) or any other shape.
	/// </summary>
	private static bool TryGetSingleReturnExpression(SyntaxNode? statement, out ExpressionSyntax expression)
	{
		expression = statement switch
		{
			ReturnStatementSyntax { Expression: { } ret } => ret,
			BlockSyntax { Statements: [ ReturnStatementSyntax { Expression: { } ret } ] } => ret,
			_ => null!
		};

		return expression is not null;
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		var names = variables.Keys.ToImmutableHashSet();

		Visit(node.Declaration);

		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			switch (value)
			{
				case false:
				{
					return null;
				}
				case true:
				{
					return TryUnrollForLoop(node);
				}
			}
		}

		return VisitForStatementWithoutUnroll(node, names);
	}

	/// <summary>
	///   Tries to unroll a for loop when the condition is always true.
	/// </summary>
	private SyntaxNode? TryUnrollForLoop(ForStatementSyntax node)
	{
		if (attribute.MaxUnrollIterations == 0)
		{
			InvalidateAssignedVariables(node);
			return base.VisitForStatement(node);
		}

		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitForStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}

			if (statement is BlockSyntax block)
			{
				result.Add(block);
			}

			VisitList(node.Incrementors);
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		return result.Count > 0 ? ToStatementSyntax(result) : null;
	}

	/// <summary>
	///   Visits a for statement without attempting to unroll it.
	/// </summary>
	private SyntaxNode VisitForStatementWithoutUnroll(ForStatementSyntax node, ImmutableHashSet<string> names)
	{
		// Restore variable states after visiting the loop
		foreach (var name in variables.Keys.Except(names).ToList())
		{
			variables.Remove(name);
		}

		var declaration = Visit(node.Declaration);

		// Visit initializers first so that initial values are set (e.g. i = 0),
		// then invalidate loop variables so the condition and body see them as unknown
		// (the variable changes on every iteration via the incrementors).
		var visitedInitializers = VisitList(node.Initializers);
		InvalidateAssignedVariables(node);
		InvalidateMutatingReceiverValues(node);

		return node
			.WithInitializers(visitedInitializers)
			.WithCondition(Visit(node.Condition) as ExpressionSyntax ?? node.Condition)
			.WithDeclaration(declaration as VariableDeclarationSyntax ?? node.Declaration)
			.WithStatement(Visit(node.Statement) as StatementSyntax ?? node.Statement);
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			switch (value)
			{
				case false:
				{
					return null;
				}
				case true when attribute.MaxUnrollIterations > 0:
				{
					return TryUnrollWhileLoop(node);
				}
			}
		}

		InvalidateAssignedVariables(node);
		InvalidateMutatingReceiverValues(node);

		var body = Visit(node.Statement);
		var rewrittenCondition = Visit(node.Condition);

		if (body is BlockSyntax { Statements: [ .., BreakStatementSyntax or ReturnStatementSyntax ] items })
		{
			return IfStatement(
				rewrittenCondition as ExpressionSyntax ?? node.Condition,
				Block(items.Take(items.Count - 1)));
		}

		return node
			.WithCondition(rewrittenCondition as ExpressionSyntax ?? node.Condition)
			.WithStatement(body as StatementSyntax ?? node.Statement);
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		// Do-while always executes at least once
		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitDoStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}

			if (statement is BlockSyntax block)
			{
				result.AddRange(block.Statements);
			}
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		// Check final condition
		var finalCondition = Visit(node.Condition);

		if (TryGetLiteralValue(finalCondition, out var finalValue) && finalValue is false)
		{
			return result.Count > 0 ? ToStatementSyntax(result) : null;
		}

		InvalidateAssignedVariables(node);
		InvalidateMutatingReceiverValues(node);

		return base.VisitDoStatement(node);
	}

	/// <summary>
	///   Tries to unroll a while loop when the condition is always true.
	/// </summary>
	private SyntaxNode? TryUnrollWhileLoop(WhileStatementSyntax node)
	{
		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitWhileStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}

			if (statement is BlockSyntax block)
			{
				result.Add(block);
			}
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		return result.Count > 0 ? ToStatementSyntax(result) : null;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var names = variables.Keys.ToImmutableHashSet();
		var collection = Visit(node.Expression);

		var items = GetForEachItems(collection);

		if (items is not null && attribute.MaxUnrollIterations > 0 && items.Count < attribute.MaxUnrollIterations)
		{
			// Save state before attempting unrolling so we can roll back when conditional
			// breaks remain in the unrolled output (i.e., break inside an if whose
			// condition could not be resolved at compile time).
			var savedState = SaveVariableState();
			var unrolled = TryUnrollForEachLoop(node, items);

			// If the unrolled result still contains break statements that are not
			// protected by any inner loop or switch, the breaks are orphaned and
			// would produce invalid C#.  Fall back to a regular foreach, using the
			// constant-folded collection expression so that partial evaluation is
			// still applied where possible.
			if (ContainsOrphanedBreak(unrolled))
			{
				RestoreVariableState(savedState);
				InvalidateAssignedVariablesForForEach(node, names);
				return base.VisitForEachStatement(
					node.WithExpression(collection as ExpressionSyntax ?? node.Expression));
			}

			return unrolled;
		}

		InvalidateAssignedVariablesForForEach(node, names);

		return base.VisitForEachStatement(node);
	}

	/// <summary>
	///   Returns <see langword="true" /> when <paramref name="node" /> contains a
	///   <see cref="BreakStatementSyntax" /> that is not enclosed by any inner loop or
	///   switch statement, making it an orphaned break with no valid jump target.
	/// </summary>
	private static bool ContainsOrphanedBreak(SyntaxNode? node)
	{
		if (node is null)
		{
			return false;
		}

		return node
			.DescendantNodes(n =>
				n is not ForStatementSyntax
					and not ForEachStatementSyntax
					and not WhileStatementSyntax
					and not DoStatementSyntax
					and not SwitchStatementSyntax)
			.OfType<BreakStatementSyntax>()
			.Any();
	}

	/// <summary>
	///   Gets the items from a foreach collection expression.
	/// </summary>
	private IReadOnlyList<CSharpSyntaxNode>? GetForEachItems(SyntaxNode? collection)
	{
		return collection switch
		{
			CollectionExpressionSyntax collectionExpression => collectionExpression.Elements,
			LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } stringLiteral =>
				stringLiteral.Token.ValueText
					.Select(CSharpSyntaxNode (s) => CreateLiteral(s))
					.ToList(),
			_ => null
		};
	}

	/// <summary>
	///   Infers the element type from the first literal in a synthetic collection (e.g. produced
	///   by LinqUnroller). Used when the semantic model cannot provide operation information.
	/// </summary>
	private ITypeSymbol? InferElementTypeFromItems(IReadOnlyList<CSharpSyntaxNode> items)
	{
		if (items.Count == 0) return null;

		var first = items[0] is ExpressionElementSyntax expElem ? expElem.Expression : items[0] as ExpressionSyntax;

		if (first is null || !TryGetLiteralValue(first, out var val) || val is null) return null;

		return InferTypeSymbolFromValue(val);
	}

	/// <summary>
	///   Tries to unroll a foreach loop.
	/// </summary>
	private SyntaxNode? TryUnrollForEachLoop(ForEachStatementSyntax node, IReadOnlyList<CSharpSyntaxNode> items)
	{
		var name = node.Identifier.Text;

		ITypeSymbol? elementType;

		if (semanticModel.GetOperation(node) is IForEachLoopOperation operation)
		{
			elementType = operation.LoopControlVariable.Type;
		}
		else
		{
			// Synthetic foreach (generated by LinqUnroller, not in original source tree) —
			// infer the element type from the first available literal item.
			elementType = InferElementTypeFromItems(items);

			if (elementType is null)
			{
				return base.VisitForEachStatement(node);
			}
		}

		var variable = new VariableItem(elementType, true, null, true);
		variables.Add(name, variable);

		var statements = new List<SyntaxNode>();

		foreach (var item in items)
		{
			if (!TryGetLiteralValue(item, out var val))
			{
				continue;
			}

			variable.Value = val;
			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				statements.Add(statement);
			}

			if (ShouldStopUnrolling(statement, statements))
			{
				break;
			}

			if (statement is BlockSyntax block)
			{
				statements.Add(block);
			}
		}

		var result = ToStatementSyntax(statements);

		// After unrolling, N consecutive equality-check if-statements with identical bodies
		// may be collapsed into a single combined if by CombineConsecutiveIfStatements
		// (e.g. "if (1==x){return true;} … if (5==x){return true;}" → "if (x is 1 or 5){return true;}").
		// Returning a single IfStatementSyntax (instead of a BlockSyntax wrapping N ifs) means
		// the parent VisitBlock will see the if as a direct sibling of the trailing "return false;"
		// and SimplifyIfReturnPatterns can then fold the pair into "return x is 1 or 5;".
		//
		// Guard: only combine when every if-statement's body unconditionally jumps
		// (return / break / continue / throw).  Without this guard, accumulation patterns like
		// "if (5==x) count++; if (5==x) count++;" (from arrays with duplicate values) would be
		// incorrectly merged into a single conditional that increments count only once.
		if (result is BlockSyntax resultBlock
		    && resultBlock.Statements.All(s => s is not IfStatementSyntax ifStmt
		                                       || ContainsJumpStatement(ifStmt.Statement)))
		{
			var combined = CombineConsecutiveIfStatements(resultBlock.Statements, Visit);
			result = combined.Count == 1 ? combined[0] : Block(combined);
		}

		return result;
	}

	/// <summary>
	///   Invalidates assigned variables for a foreach loop.
	/// </summary>
	private void InvalidateAssignedVariablesForForEach(ForEachStatementSyntax node, ImmutableHashSet<string> names)
	{
		var assignedVariables = AssignedVariables(node);

		foreach (var name in names)
		{
			if (variables.TryGetValue(name, out var variable) && assignedVariables.Contains(name))
			{
				variable.HasValue = false;
			}
		}
	}

	/// <summary>
	///   Checks if loop unrolling should stop based on the current statement.
	/// </summary>
	private static bool ShouldStopUnrolling(SyntaxNode? statement, ICollection<SyntaxNode?> result)
	{
		if (statement is BreakStatementSyntax or ReturnStatementSyntax)
		{
			return true;
		}

		if (statement is not BlockSyntax block || !block.Statements.Any(s => s is BreakStatementSyntax or ReturnStatementSyntax))
		{
			return false;
		}

		foreach (var item in block.Statements)
		{
			if (item is BreakStatementSyntax)
			{
				return true;
			}

			result.Add(item);

			if (item is ReturnStatementSyntax)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	///   Invalidates all assigned variables in the given node.
	/// </summary>
	private void InvalidateAssignedVariables(StatementSyntax node)
	{
		foreach (var name in AssignedVariables(node))
		{
			if (variables.TryGetValue(name, out var variable))
			{
				variable.HasValue = false;
			}
		}
	}

	private void InvalidateMutatingReceiverValues(StatementSyntax node)
	{
		foreach (var invocation in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			if (invocation.Expression is not MemberAccessExpressionSyntax
			    {
				    Expression: IdentifierNameSyntax { Identifier.Text: var receiverName },
				    Name: IdentifierNameSyntax { Identifier.Text: var methodName }
			    })
			{
				continue;
			}

			if (!variables.TryGetValue(receiverName, out var receiverVariable))
			{
				continue;
			}

			if (!semanticModel.TryGetSymbol(invocation, symbolStore, out IMethodSymbol? targetMethod)
			    && !IsLikelyMutatingMethod(null, methodName))
			{
				continue;
			}

			if (targetMethod is not null && !IsLikelyMutatingMethod(targetMethod, methodName))
			{
				continue;
			}

			receiverVariable.IsAltered = true;
			receiverVariable.HasValue = false;
		}
	}

	public override SyntaxNode VisitBlock(BlockSyntax node)
	{
		var visited = VisitList(node.Statements);
		var mergedForDeclarations = MergeForLoopDeclarations(visited);
		var mergedArrayInitializers = MergeArrayElementInitializers(mergedForDeclarations);
		var liftedDeclarations = MergeUninitializedDeclarations(mergedArrayInitializers);
		var mergedInitializers = MergeRedundantInitializers(liftedDeclarations);

		var untilThrown = TakeUntilThrownStatements(mergedInitializers);
		var combined = CombineConsecutiveIfStatements(untilThrown, Visit);
		var mergedIfChain = MergeMixedBoolReturnIfs(combined, Visit);
		var simplified = SimplifyIfReturnPatterns(mergedIfChain);
		var switched = CombineConsecutiveIfsIntoSwitch(simplified);
		var inlined = InlineSingleUseLocalVariables(switched);

		return node.WithStatements(inlined);
	}

	/// <summary>
	///   Inlines local variables that are declared once, never reassigned, and used in exactly
	///   one straight-line read in the same block. For example:
	///   <code>
	/// var diff = a - b;
	/// return Int32.Abs(diff);
	/// </code>
	///   becomes:
	///   <code>
	/// return Int32.Abs(a - b);
	/// </code>
	///   Only single-use variables whose read is not inside a loop or lambda are inlined
	///   (to avoid changing how often the initializer expression is evaluated).
	/// </summary>
	private SyntaxList<StatementSyntax> InlineSingleUseLocalVariables(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = statements.ToList();

		for (var i = 0; i < result.Count - 1; i++)
		{
			if (result[i] is not LocalDeclarationStatementSyntax
			    {
				    Modifiers.Count: 0,
				    Declaration.Variables: [ { Identifier.Text: var varName, Initializer.Value: ExpressionSyntax initExpr } ]
			    })
			{
				continue;
			}

			// Only inline pure arithmetic/logical expressions. Complex initializers (array or
			// object creation, method calls, element access) are kept as named variables so the
			// intent remains clear and the optimiser's own structural passes are not undone.
			if (!IsInlineableExpression(initExpr))
			{
				continue;
			}

			var remaining = result.Skip(i + 1).ToList();
			var collector = new VariableUsageCollector([ varName ]);

			foreach (var s in remaining)
			{
				collector.Visit(s);
			}

			if (collector.GetReadCount(varName) != 1
			    || collector.GetWriteCount(varName) > 0
			    || collector.GetRefCount(varName) > 0)
			{
				continue;
			}

			// Don't inline when the single use is inside a loop or lambda — inlining would
			// change how often the initializer expression is evaluated.
			if (IsUsedInsideLoopOrLambda(varName, remaining))
			{
				continue;
			}

			// Don't inline when any identifier referenced by the initializer is subsequently
			// mutated — inlining would observe the mutated value instead of the original.
			if (IsAnyInitExprIdentifierMutated(initExpr, remaining))
			{
				continue;
			}

			// Replace the single occurrence of varName with the initializer expression.
			var inliner = new SingleUseInliner(varName, initExpr);
			var inlinedRemaining = remaining.Select(s => (StatementSyntax)(inliner.Visit(s) ?? s)).ToList();

			result.RemoveAt(i);

			for (var j = 0; j < inlinedRemaining.Count; j++)
			{
				result[i + j] = inlinedRemaining[j];
			}

			i--;
		}

		return List(result);
	}

	/// <summary>
	///   Returns <see langword="true" /> when <paramref name="expr" /> is a pure value expression
	///   (binary or unary arithmetic/logical) that is safe to inline at a single use site.
	///   Complex expressions (array/object creation, method calls, element access) are excluded
	///   so that structural optimisation passes are not silently undone.
	/// </summary>
	private static bool IsInlineableExpression(ExpressionSyntax expr)
	{
		return expr is BinaryExpressionSyntax or PrefixUnaryExpressionSyntax or CastExpressionSyntax
			or ConditionalExpressionSyntax
			or ImplicitArrayCreationExpressionSyntax or ArrayCreationExpressionSyntax;
	}

	/// <summary>
	///   Returns <see langword="true" /> when at least one identifier referenced by
	///   <paramref name="initExpr" /> is written to in any of the <paramref name="remaining" /> statements.
	///   Inlining is unsafe in that case because the written-to identifier's value would differ
	///   from the value it held at the point of declaration.
	/// </summary>
	private static bool IsAnyInitExprIdentifierMutated(ExpressionSyntax initExpr, IEnumerable<StatementSyntax> remaining)
	{
		var names = new HashSet<string>(
			initExpr
				.DescendantNodesAndSelf()
				.OfType<IdentifierNameSyntax>()
				.Select(id => id.Identifier.Text));

		if (names.Count == 0)
		{
			return false;
		}

		var collector = new VariableUsageCollector(names);

		foreach (var s in remaining)
		{
			collector.Visit(s);
		}

		return names.Any(name => collector.GetWriteCount(name) > 0);
	}

	private static bool IsUsedInsideLoopOrLambda(string varName, IEnumerable<StatementSyntax> statements)
	{
		foreach (var s in statements)
		{
			foreach (var node in s.DescendantNodes())
			{
				if (node is not (ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax
				    or DoStatementSyntax or LambdaExpressionSyntax or AnonymousMethodExpressionSyntax))
				{
					continue;
				}

				var nestedCollector = new VariableUsageCollector([ varName ]);
				nestedCollector.Visit(node);

				if (nestedCollector.GetReadCount(varName) > 0)
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	///   Merges a run of independent sibling if-statements (no else) that all test the same
	///   identifier against mutually-exclusive constant / relational patterns into a single
	///   switch statement. Example:
	///   <code>
	/// if (n == 0) return 1;        switch (n)
	/// if (n &lt; 0)  n = -n;    =>   {
	///                                  case 0:   return 1;
	///                                  case &lt; 0: n = -n; break;
	///                              }
	/// </code>
	///   Guards (all required to preserve semantics):
	///   <list type="bullet">
	///     <item>
	///       at least two cases, and no body relies on an orphaned <c>break</c> — a switch captures
	///       <c>break</c>, so a <c>break</c> bound to an enclosing loop would change meaning inside a
	///       case (<c>return</c>/<c>throw</c>/<c>continue</c> are unaffected);
	///     </item>
	///     <item>the case patterns are mutually exclusive, so at most one body ever runs;</item>
	///     <item>
	///       only the last section may write to the target — a switch reads the target once
	///       while sequential ifs re-evaluate it against the mutated value.
	///     </item>
	///   </list>
	/// </summary>
	private static SyntaxList<StatementSyntax> CombineConsecutiveIfsIntoSwitch(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var i = 0;

		while (i < statements.Count)
		{
			if (statements[i] is IfStatementSyntax { Else: null } firstIf
			    && ConvertIfToSwitchCodeRefactoring.TryGetConsecutiveIfSection(firstIf, out var firstSection))
			{
				var targetName = firstSection.Target.Identifier.ValueText;
				var sections = new List<ConvertIfToSwitchCodeRefactoring.IfSwitchSection> { firstSection };
				var j = i + 1;

				while (j < statements.Count
				       && statements[j] is IfStatementSyntax { Else: null } nextIf
				       && ConvertIfToSwitchCodeRefactoring.TryGetConsecutiveIfSection(nextIf, out var nextSection)
				       && nextSection.Target.Identifier.ValueText == targetName
				       && sections.All(s => ConvertIfToSwitchCodeRefactoring.AreMutuallyExclusive(s, nextSection))
				       // Appending another case turns every already-collected section into a
				       // non-last one, so none of them may mutate the target.
				       && sections.All(s => !ConvertIfToSwitchCodeRefactoring.AssignsToIdentifier(s.Body, targetName)))
				{
					sections.Add(nextSection);
					j++;
				}

				if (sections.Count >= 2
				    // A switch captures `break`, so a body whose control flow relies on an
				    // orphaned `break` (one bound to an enclosing loop, not a nested loop/switch)
				    // would change meaning inside a case. `return`/`throw`/`continue` are unaffected,
				    // so all-jump runs of those are safe to fold into a switch. A bare `break;` body
				    // is checked explicitly because ContainsOrphanedBreak only scans descendants.
				    && sections.All(s => s.Body is not BreakStatementSyntax && !ContainsOrphanedBreak(s.Body))
				    && ConvertIfToSwitchCodeRefactoring.TryBuildConsecutiveIfsSwitch(firstSection.Target, sections, out var switchStatement))
				{
					result.Add(switchStatement);
					i = j;
					continue;
				}
			}

			result.Add(statements[i]);
			i++;
		}

		return List(result);
	}

	/// <summary>
	///   Merges uninitialized variable declarations with their first direct assignment in the
	///   same block. For example:
	///   <code>
	/// double delta, min;
	/// min = Compute();
	/// delta = x - min;
	///   </code>
	///   becomes:
	///   <code>
	/// var min = Compute();
	/// var delta = x - min;
	///   </code>
	///   Only top-level simple assignments are merged; assignments inside branches or loops are
	///   left unchanged so that control-flow semantics are preserved.
	/// </summary>
	private static SyntaxList<StatementSyntax> MergeUninitializedDeclarations(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
			return statements;

		var result = statements.ToList();

		for (var i = 0; i < result.Count; i++)
		{
			if (result[i] is not LocalDeclarationStatementSyntax { Modifiers.Count: 0 } declStmt)
				continue;

			var uninitializedNames = new HashSet<string>(
				declStmt.Declaration.Variables
					.Where(v => v.Initializer == null)
					.Select(v => v.Identifier.Text));

			if (uninitializedNames.Count == 0)
				continue;

			// Find the first direct assignment to each uninitialized variable
			var mergedByIndex = new Dictionary<int, string>();

			foreach (var varName in uninitializedNames)
			{
				for (var j = i + 1; j < result.Count; j++)
				{
					if (result[j] is ExpressionStatementSyntax
					    {
						    Expression: AssignmentExpressionSyntax
						    {
							    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
							    Left: IdentifierNameSyntax { Identifier.Text: var assignedName }
						    }
					    }
					    && assignedName == varName)
					{
						mergedByIndex[j] = varName;
						break;
					}
				}
			}

			if (mergedByIndex.Count == 0)
				continue;

			// Lift each matched assignment to a var declaration at its current position
			foreach (var kvp in mergedByIndex)
			{
				var assignment = (AssignmentExpressionSyntax)((ExpressionStatementSyntax)result[kvp.Key]).Expression;
				var newDeclarator = VariableDeclarator(Identifier(kvp.Value))
					.WithInitializer(EqualsValueClause(assignment.Right));
				result[kvp.Key] = LocalDeclarationStatement(
					VariableDeclaration(ParseTypeName("var"))
						.WithVariables(SingletonSeparatedList(newDeclarator)));
			}

			// Remove merged vars from the original declaration; keep any that had initializers
			var mergedNames = new HashSet<string>(mergedByIndex.Values);
			var remainingVars = declStmt.Declaration.Variables
				.Where(v => !mergedNames.Contains(v.Identifier.Text))
				.ToList();

			if (remainingVars.Count == 0)
			{
				result.RemoveAt(i);
				i--;
			}
			else
			{
				var newType = remainingVars.Count == 1 ? ParseTypeName("var") : declStmt.Declaration.Type;
				result[i] = declStmt.WithDeclaration(
					declStmt.Declaration
						.WithType(newType)
						.WithVariables(SeparatedList(remainingVars)));
			}
		}

		return List(result);
	}

	/// <summary>
	///   Merges a single-variable declaration whose initializer is a side-effect-free constant with an
	///   immediately following unconditional simple assignment to that same variable. For example:
	///   <code>
	/// double hue = 0.0;
	/// hue = normalizedR == max ? a : b;
	///   </code>
	///   becomes:
	///   <code>
	/// var hue = normalizedR == max ? a : b;
	///   </code>
	///   This collapses the dead initial value that remains after an if/else assignment chain has been
	///   folded into a conditional expression. The transform is only applied when:
	///   <list type="bullet">
	///     <item>
	///       the declaration is a single, unmodified variable with a side-effect-free initializer
	///       (a literal / default / unary-signed literal), so dropping the initial store is safe;
	///     </item>
	///     <item>
	///       the assignment is the <em>immediately</em> following statement, so nothing can read the
	///       initial value before it is overwritten;
	///     </item>
	///     <item>
	///       the assignment's right-hand side does not read the variable itself — otherwise it would
	///       observe the (now removed) initial value.
	///     </item>
	///   </list>
	/// </summary>
	private static SyntaxList<StatementSyntax> MergeRedundantInitializers(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var i = 0;

		while (i < statements.Count)
		{
			if (i + 1 < statements.Count
			    && statements[i] is LocalDeclarationStatementSyntax
			    {
				    Modifiers.Count: 0,
				    Declaration.Variables: [ { Identifier.Text: var name, Initializer.Value: var initializer } declarator ]
			    } declarationStatement
			    && IsSideEffectFreeInitializer(initializer)
			    && statements[i + 1] is ExpressionStatementSyntax
			    {
				    Expression: AssignmentExpressionSyntax
				    {
					    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
					    Left: IdentifierNameSyntax { Identifier.Text: var assignedName },
					    Right: var rhs
				    }
			    }
			    && assignedName == name
			    && !rhs.HasIdentifier(name))
			{
				var mergedDeclarator = declarator.WithInitializer(EqualsValueClause(rhs));

				result.Add(declarationStatement.WithDeclaration(
					declarationStatement.Declaration
						.WithType(ParseTypeName("var"))
						.WithVariables(SingletonSeparatedList(mergedDeclarator))));

				i += 2;
				continue;
			}

			result.Add(statements[i]);
			i++;
		}

		return List(result);
	}

	/// <summary>
	///   Returns <see langword="true" /> when an initializer is a side-effect-free constant whose dead
	///   store can be dropped safely (a literal, a <c>default</c> expression, or a unary <c>+</c>/<c>-</c>
	///   applied to a literal).
	/// </summary>
	private static bool IsSideEffectFreeInitializer(ExpressionSyntax? initializer)
	{
		return initializer switch
		{
			LiteralExpressionSyntax => true,
			DefaultExpressionSyntax => true,
			PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax } => true,
			_ => false
		};
	}

	private SyntaxList<StatementSyntax> MergeForLoopDeclarations(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var comparer = SyntaxNodeComparer.Get<ExpressionSyntax>();

		for (var i = 0; i < statements.Count; i++)
		{
			if (i + 1 < statements.Count
			    && statements[i] is LocalDeclarationStatementSyntax
			    {
				    Modifiers.Count: 0,
				    Declaration:
				    {
					    Variables: [ { Identifier.Text: var variableName, Initializer: { Value: var declaredValue } } declarator ]
				    } declaration
			    }
			    && statements[i + 1] is ForStatementSyntax
			    {
				    Declaration: null,
				    Initializers:
				    [
					    AssignmentExpressionSyntax
					    {
						    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
						    Left: IdentifierNameSyntax { Identifier.Text: var initializerName },
						    Right: var initializerValue
					    }
				    ]
			    } forStatement
			    && variableName == initializerName
			    && comparer.Equals(declaredValue.WithoutTrivia(), initializerValue.WithoutTrivia())
			    && !statements.Skip(i + 2).Any(statement => statement.HasIdentifier(variableName)))
			{
				var mergedDeclaration = declaration.WithVariables(
					SeparatedList([
						declarator.WithInitializer(EqualsValueClause(initializerValue))
					]));

				result.Add(forStatement
					.WithDeclaration(mergedDeclaration)
					.WithInitializers(default));
				i++;
				continue;
			}

			result.Add(statements[i]);
		}

		return List(result);
	}

	/// <summary>
	///   Merges an array declaration of constant size followed by a contiguous run of constant-index
	///   element assignments into a single array initializer.
	///   <code>
	/// var result = new int[6];
	/// result[0] = a;
	/// result[1] = b;
	/// ...
	/// </code>
	///   becomes
	///   <code>
	/// var result = new int[]
	/// {
	///     a,
	///     b,
	///     ...
	/// };
	/// </code>
	///   The transform is only applied when every index <c>0..N-1</c> is assigned exactly once, in order,
	///   immediately after the declaration, and no assigned value reads the array being built. The
	///   contiguity requirement guarantees nothing can observe the zero-filled array before it is fully
	///   populated, so the rewrite is semantics-preserving.
	/// </summary>
	private SyntaxList<StatementSyntax> MergeArrayElementInitializers(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();

		for (var i = 0; i < statements.Count; i++)
		{
			if (TryMergeArrayElementInitializer(statements, i, out var merged, out var consumed))
			{
				result.Add(merged);
				i += consumed - 1;
				continue;
			}

			result.Add(statements[i]);
		}

		return List(result);
	}

	private bool TryMergeArrayElementInitializer(SyntaxList<StatementSyntax> statements, int declIndex, out StatementSyntax merged, out int consumed)
	{
		merged = null!;
		consumed = 0;

		// The declaration must be a single, unmodified `name = new T[size]` without an existing
		// initializer and with exactly one rank specifier carrying one size expression.
		if (statements[declIndex] is not LocalDeclarationStatementSyntax
		    {
			    Modifiers.Count: 0,
			    Declaration.Variables:
			    [
				    {
					    Identifier.Text: var name,
					    Initializer.Value: ArrayCreationExpressionSyntax
					    {
						    Type.RankSpecifiers: [ { Sizes: [ var sizeExpr ] } rankSpecifier ],
						    Initializer: null
					    } arrayCreation
				    } declarator
			    ]
		    } declarationStatement
		    || sizeExpr is OmittedArraySizeExpressionSyntax
		    || !TryGetConstantInt32(sizeExpr, out var size)
		    || size <= 0
		    || declIndex + size >= statements.Count)
		{
			return false;
		}

		var elements = new List<ExpressionSyntax>(size);

		for (var k = 0; k < size; k++)
		{
			// Each follow-up statement must assign `name[k] = value`, with k matching the position
			// and value never reading the array that is still being initialized.
			if (statements[declIndex + 1 + k] is not ExpressionStatementSyntax
			    {
				    Expression: AssignmentExpressionSyntax
				    {
					    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
					    Left: ElementAccessExpressionSyntax
					    {
						    Expression: IdentifierNameSyntax { Identifier.Text: var targetName },
						    ArgumentList.Arguments: [ { Expression: var indexExpr } ]
					    },
					    Right: var valueExpr
				    }
			    }
			    || targetName != name
			    || !TryGetConstantInt32(indexExpr, out var index)
			    || index != k
			    || valueExpr.HasIdentifier(name))
			{
				return false;
			}

			elements.Add(valueExpr);
		}

		var implicitlySizedType = arrayCreation.Type.WithRankSpecifiers(
			SingletonList(rankSpecifier.WithSizes(
				SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression()))));

		var elementList = SeparatedList(elements);

		var newArrayCreation = ImplicitArrayCreationExpression(InitializerExpression(SyntaxKind.ArrayInitializerExpression, elementList));
		var newDeclarator = declarator.WithInitializer(EqualsValueClause(newArrayCreation));

		merged = declarationStatement.WithDeclaration(
			declarationStatement.Declaration.WithVariables(SingletonSeparatedList(newDeclarator)));

		consumed = size + 1;
		return true;
	}

	private bool TryGetConstantInt32(ExpressionSyntax? expr, out int value)
	{
		value = 0;

		if (!TryGetLiteralValue(expr, out var literal))
		{
			return false;
		}

		try
		{
			value = Convert.ToInt32(literal);
			return true;
		}
		catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
		{
			return false;
		}
	}

	/// <summary>
	///   Takes statements until a throw or return statement is encountered (inclusive).
	///   Any code after a throw or return statement is unreachable and can be removed.
	/// </summary>
	private static SyntaxList<StatementSyntax> TakeUntilThrownStatements(SyntaxList<StatementSyntax> statements)
	{
		var result = new List<StatementSyntax>();

		foreach (var statement in statements)
		{
			result.Add(statement);

			// Stop after a throw or return statement since code after it is unreachable
			if (statement is ThrowStatementSyntax
			    or ExpressionStatementSyntax { Expression: ThrowExpressionSyntax }
			    or ReturnStatementSyntax)
			{
				break;
			}
		}

		// Strip a trailing void return (return;) — it is redundant at the end of a void block
		// and produces cleaner output (e.g. empty body {} instead of { return; }).
		if (result.Count > 0 && result[^1] is ReturnStatementSyntax { Expression: null })
		{
			result.RemoveAt(result.Count - 1);
		}

		return List(result);
	}

	/// <summary>
	///   Simplifies patterns like:
	///   - if (cond) { return true; } return false; => return cond;
	///   - if (cond) { return false; } return true; => return !cond;
	/// </summary>
	private SyntaxList<StatementSyntax> SimplifyIfReturnPatterns(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();

		for (var i = 0; i < statements.Count; i++)
		{
			// Check for pattern: if (cond) { return <bool>; } followed by return <opposite bool>;
			if (i + 1 < statements.Count
			    && statements[i] is IfStatementSyntax { Else: null } ifStatement
			    && statements[i + 1] is ReturnStatementSyntax followingReturn)
			{
				if (TryGetIfReturnBoolPattern(ifStatement, followingReturn, out var simplifiedReturn)
				    || TryGetIfReturnPattern(ifStatement, followingReturn, out simplifiedReturn))
				{
					result.Add(simplifiedReturn!);
					i++; // Skip the following return statement
					continue;
				}
			}

			result.Add(statements[i]);
		}

		var resultList = List(result);

		if (statements.Count != resultList.Count)
		{
			return SimplifyIfReturnPatterns(resultList);
		}

		return resultList;
	}

	/// <summary>
	///   Tries to simplify if-return-bool patterns.
	/// </summary>
	private bool TryGetIfReturnBoolPattern(IfStatementSyntax ifStatement, ReturnStatementSyntax followingReturn, out ReturnStatementSyntax? simplified)
	{
		simplified = null;

		// Get the return statement from the if body
		var ifBody = ifStatement.Statement;

		var ifReturn = ifBody switch
		{
			ReturnStatementSyntax ret => ret,
			BlockSyntax { Statements: [ ReturnStatementSyntax ret ] } => ret,
			_ => null
		};

		if (ifReturn is null)
		{
			return false;
		}

		// Check if both returns are boolean literals
		if (!TryGetBoolLiteral(ifReturn.Expression, out var ifReturnValue))
		{
			return false;
		}

		// Only simplify if they return opposite values
		if (SyntaxNodeComparer.Get().Equals(ifReturn.Expression, followingReturn.Expression))
		{
			return false;
		}

		// if (cond) { return true; } return false; => return cond;
		// if (cond) { return false; } return true; => return !cond;
		var condition = ifStatement.Condition;

		if (ifReturnValue)
		{
			if (followingReturn.Expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression })
			{
				// return cond;
				simplified = ReturnStatement(condition);
			}
			else
			{
				var newCondition = LogicalAndExpression(condition, followingReturn.Expression);
				var booleanType = semanticModel.Compilation.CreateBoolean();

				if (TryOptimizeNode(BinaryOperatorKind.ConditionalAnd, [ ], booleanType, condition, booleanType, followingReturn.Expression, booleanType, null, out var result))
				{
					simplified = ReturnStatement(result as ExpressionSyntax ?? condition);
					return true;
				}

				simplified = ReturnStatement(newCondition);
			}
		}
		else if (InvertLogicalRefactoring.TryInvertLogical(condition as BinaryExpressionSyntax, out var inverted))
		{
			var booleanType = semanticModel.Compilation.CreateBoolean();

			if (TryOptimizeNode(BinaryOperatorKind.ConditionalAnd, [ ], booleanType, inverted, booleanType, followingReturn.Expression, booleanType, null, out var result))
			{
				simplified = ReturnStatement(result as ExpressionSyntax ?? condition);
				return true;
			}

			simplified = ReturnStatement(LogicalAndExpression(inverted, followingReturn.Expression));
		}
		else
		{
			var booleanType = semanticModel.Compilation.CreateBoolean();
			var invertedCondition = NegateExpressionRefactoring.Negate(condition);

			if (TryOptimizeNode(BinaryOperatorKind.ConditionalAnd, [ ], booleanType, invertedCondition, booleanType, followingReturn.Expression, booleanType, null, out var result))
			{
				simplified = ReturnStatement(result as ExpressionSyntax ?? condition);
				return true;
			}

			simplified = ReturnStatement(LogicalAndExpression(invertedCondition, followingReturn.Expression));
		}

		return simplified is not null;
	}

	/// <summary>
	///   Tries to simplify if-return-bool patterns.
	/// </summary>
	private bool TryGetIfReturnPattern(IfStatementSyntax ifStatement, ReturnStatementSyntax followingReturn, out ReturnStatementSyntax? simplified)
	{
		simplified = null;

		// Get the return statement from the if body
		var ifBody = ifStatement.Statement;

		var ifReturn = ifBody switch
		{
			ReturnStatementSyntax ret => ret,
			BlockSyntax { Statements: [ ReturnStatementSyntax ret ] } => ret,
			_ => null
		};

		if (ifReturn is null)
		{
			return false;
		}

		if (!semanticModel.TryGetTypeSymbol(ifReturn.Expression, symbolStore, out var ifReturnType)
		    && !semanticModel.TryGetTypeSymbol(followingReturn.Expression, symbolStore, out ifReturnType))
		{
			simplified = ReturnStatement(Visit(
				ConditionalExpression(
					ifStatement.Condition,
					ifReturn.Expression,
					followingReturn.Expression)) as ExpressionSyntax ?? ifReturn.Expression);
		}
		else
		{
			simplified = ReturnStatement(Visit(
				ConditionalExpression(
					ifStatement.Condition,
					ifReturn.Expression,
					followingReturn.Expression).WithTypeSymbolAnnotation(ifReturnType, symbolStore)) as ExpressionSyntax ?? ifReturn.Expression);
		}

		return true;
	}

	/// <summary>
	///   Tries to extract a boolean literal value from an expression.
	/// </summary>
	private static bool TryGetBoolLiteral(ExpressionSyntax? expression, out bool value)
	{
		value = false;

		if (expression is LiteralExpressionSyntax literal)
		{
			if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
			{
				value = true;
				return true;
			}

			if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
			{
				value = false;
				return true;
			}
		}

		return false;
	}

	/// <summary>
	///   Merges a consecutive run of if-statements (each returning a boolean literal, no else)
	///   that contains a mix of <c>return true</c> and <c>return false</c> into a single
	///   <c>if (...) return false;</c> statement.
	///   <para>
	///     For each if-statement in the run:
	///     <list type="bullet">
	///       <item>If it returns <c>false</c> → its condition is added to the OR chain as-is.</item>
	///       <item>
	///         If it returns <c>true</c>  → its condition is negated and added to the OR chain,
	///         because reaching a subsequent <c>return false</c> requires the <c>return true</c>
	///         guard to have been skipped (i.e., its condition was false).
	///       </item>
	///     </list>
	///   </para>
	///   Example:
	///   <code>
	/// if (n &lt;= 1) return false;
	/// if (n &lt;= 3) return true;
	/// if (IsEven(n) || n % 3 == 0) return false;
	/// </code>
	///   becomes:
	///   <code>
	/// if (n &lt;= 1 || n &gt; 3 || IsEven(n) || n % 3 == 0) return false;
	/// </code>
	/// </summary>
	private static SyntaxList<StatementSyntax> MergeMixedBoolReturnIfs(SyntaxList<StatementSyntax> statements, Func<SyntaxNode?, SyntaxNode?> visit)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var i = 0;

		while (i < statements.Count)
		{
			var runStart = i;
			var run = new List<(ExpressionSyntax Condition, bool ReturnValue)>();

			while (i < statements.Count && TryGetIfBoolReturnBody(statements[i], out var cond, out var retVal))
			{
				run.Add((cond!, retVal));
				i++;
			}

			// Only merge when there are at least 2 ifs with mixed true/false returns
			if (run.Count >= 2 && run.Any(r => r.ReturnValue) && run.Any(r => !r.ReturnValue))
			{
				// Build combined OR condition:
				// - "return false" conditions → keep as-is
				// - "return true" conditions → negate (they act as guards; skipping them means we fall through to the false path)
				ExpressionSyntax? combined = null;

				foreach (var (cond, retVal) in run)
				{
					var part = retVal ? NegateCondition(cond) : cond;

					if (combined is null)
					{
						combined = part;
					}
					else
					{
						combined = LogicalOrExpression(
							NeedsParenthesesInOrContext(combined) ? ParenthesizedExpression(combined) : combined,
							NeedsParenthesesInOrContext(part) ? ParenthesizedExpression(part) : part);
					}
				}

				var visitedCombined = visit(combined) as ExpressionSyntax ?? combined!;

				result.Add(IfStatement(
					visitedCombined,
					ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))));
			}
			else if (run.Count > 0)
			{
				// Run is all-same-value — add back unchanged
				for (var k = runStart; k < runStart + run.Count; k++)
				{
					result.Add(statements[k]);
				}
			}
			else
			{
				// Current statement is not a bool-return if — add it as-is and advance
				result.Add(statements[i]);
				i++;
			}
		}

		return List(result);
	}

	/// <summary>
	///   Tries to extract the condition and boolean return value from an if-statement whose body
	///   is a single <c>return true;</c> or <c>return false;</c> (with or without braces) and
	///   that has no else clause.
	/// </summary>
	private static bool TryGetIfBoolReturnBody(
		StatementSyntax statement,
		out ExpressionSyntax? condition,
		out bool returnValue)
	{
		condition = null;
		returnValue = false;

		if (statement is not IfStatementSyntax { Else: null } ifStmt)
		{
			return false;
		}

		var ret = ifStmt.Statement switch
		{
			ReturnStatementSyntax r => r,
			BlockSyntax { Statements: [ ReturnStatementSyntax r ] } => r,
			_ => null
		};

		if (ret?.Expression is not LiteralExpressionSyntax lit)
		{
			return false;
		}

		if (lit.IsKind(SyntaxKind.TrueLiteralExpression))
		{
			condition = ifStmt.Condition;
			returnValue = true;
			return true;
		}

		if (lit.IsKind(SyntaxKind.FalseLiteralExpression))
		{
			condition = ifStmt.Condition;
			returnValue = false;
			return true;
		}

		return false;
	}

	/// <summary>
	///   Negates a condition, preferring a direct inversion (e.g., <c>n &lt;= 3</c> → <c>n &gt; 3</c>)
	///   over wrapping in <c>!(…)</c>.
	/// </summary>
	private static ExpressionSyntax NegateCondition(ExpressionSyntax condition)
	{
		if (InvertLogicalRefactoring.TryInvertLogical(condition as BinaryExpressionSyntax, out var inverted))
		{
			return inverted;
		}

		return NegateExpressionRefactoring.Negate(condition);
	}

	/// <summary>
	///   Combines consecutive if statements that have identical bodies into a single if statement.
	///   Two strategies are applied in order:
	///   1. Equality pattern: conditions of the form <c>x == literal</c> against the same variable
	///   are combined into <c>if (x is 1 or 5) { … }</c>.
	///   2. General ||: when the body ends with a jump statement (return / break / continue / throw),
	///   any consecutive if statements with an identical body are combined using <c>||</c>.
	///   Example (strategy 1): if (1 == x) { return true; } if (5 == x) { return true; }
	///   => if (x is 1 or 5) { return true; }
	///   Example (strategy 2): if (x &gt; 5) { return; } if (y &lt; 3) { return; }
	///   => if (x &gt; 5 || y &lt; 3) { return; }
	/// </summary>
	internal static SyntaxList<StatementSyntax> CombineConsecutiveIfStatements(SyntaxList<StatementSyntax> statements, Func<SyntaxNode?, SyntaxNode?> visit)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var i = 0;

		while (i < statements.Count)
		{
			// Only process if statements without an else clause
			if (statements[i] is not IfStatementSyntax { Else: null } currentIf)
			{
				result.Add(statements[i]);
				i++;

				continue;
			}

			// Strategy 1: equality (is or) combination
			if (TryGetEqualityComparisonInfo(currentIf.Condition, out var targetIdentifier, out var firstLiteral))
			{
				var literals = new List<LiteralExpressionSyntax> { firstLiteral! };
				var j = i + 1;

				while (j < statements.Count)
				{
					if (statements[j] is not IfStatementSyntax { Else: null } nextIf)
					{
						break;
					}

					if (!SyntaxNodeComparer.Get<StatementSyntax>().Equals(nextIf.Statement, currentIf.Statement))
					{
						break;
					}

					if (!TryGetEqualityComparisonInfo(nextIf.Condition, out var nextTarget, out var nextLiteral)
					    || nextTarget != targetIdentifier)
					{
						break;
					}

					literals.Add(nextLiteral!);
					j++;
				}

				if (literals.Count > 1)
				{
					// e.g. target is 1 or 5 or 10
					var combinedCondition = visit(CreateIsOrPattern(targetIdentifier!, literals)) as ExpressionSyntax;
					result.Add(currentIf.WithCondition(combinedCondition));
					i = j;
					continue;
				}
			}

			// Strategy 2: general || combination when body ends with a jump statement
			if (ContainsJumpStatement(currentIf.Statement))
			{
				var conditions = new List<ExpressionSyntax> { currentIf.Condition };
				var j = i + 1;

				while (j < statements.Count)
				{
					if (statements[j] is not IfStatementSyntax { Else: null } nextIf)
					{
						break;
					}

					if (!SyntaxNodeComparer.Get<StatementSyntax>().Equals(nextIf.Statement, currentIf.Statement))

					{
						break;
					}

					conditions.Add(nextIf.Condition);
					j++;
				}

				if (conditions.Count > 1)
				{
					var combinedCondition = conditions[0];

					for (var k = 1; k < conditions.Count; k++)
					{
						combinedCondition = LogicalOrExpression(
							NeedsParenthesesInOrContext(combinedCondition)
								? ParenthesizedExpression(combinedCondition)
								: combinedCondition,
							NeedsParenthesesInOrContext(conditions[k])
								? ParenthesizedExpression(conditions[k])
								: conditions[k]);
					}

					result.Add(currentIf.WithCondition(combinedCondition));
					i = j;
					continue;
				}
			}

			result.Add(statements[i]);
			i++;
		}

		return List(result);
	}

	/// <summary>
	///   Returns <see langword="true" /> when a statement unconditionally ends with a jump
	///   (return, break, continue, or throw), making it safe to combine consecutive if
	///   statements with identical bodies using <c>||</c>.
	/// </summary>
	private static bool ContainsJumpStatement(StatementSyntax statement)
	{
		return statement switch
		{
			ReturnStatementSyntax => true,
			BreakStatementSyntax => true,
			ContinueStatementSyntax => true,
			ThrowStatementSyntax => true,
			BlockSyntax block => block.Statements.Count > 0 && ContainsJumpStatement(block.Statements.Last()),
			_ => false
		};
	}

	/// <summary>
	///   Returns <see langword="true" /> when an expression needs parentheses when used as an
	///   operand of <c>||</c> (i.e., its precedence is lower than logical-or).
	/// </summary>
	private static bool NeedsParenthesesInOrContext(ExpressionSyntax expression)
	{
		return expression is ConditionalExpressionSyntax or AssignmentExpressionSyntax;
	}

	/// <summary>
	///   Creates an 'is' pattern expression with 'or' for multiple values.
	///   Example: target is 1 or 5 or 10
	/// </summary>
	private static ExpressionSyntax CreateIsOrPattern(string targetIdentifier, List<LiteralExpressionSyntax> literals)
	{
		// Build pattern: 1 or 5 or 10 or ...
		PatternSyntax pattern = ConstantPattern(literals[0]);

		for (var k = 1; k < literals.Count; k++)
		{
			pattern = BinaryPattern(
				SyntaxKind.OrPattern,
				pattern,
				ConstantPattern(literals[k]));
		}

		// Create: target is <pattern>
		return IsPatternExpression(
			IdentifierName(targetIdentifier),
			pattern);
	}

	/// <summary>
	///   Tries to extract the comparison target identifier and literal value from an equality expression.
	///   Handles both 'value == target' and 'target == value' formats.
	/// </summary>
	private static bool TryGetEqualityComparisonInfo(ExpressionSyntax condition, out string? targetIdentifier, out LiteralExpressionSyntax? literal)
	{
		targetIdentifier = null;
		literal = null;

		if (condition is not BinaryExpressionSyntax binary
		    || !binary.IsKind(SyntaxKind.EqualsExpression))
		{
			return false;
		}

		switch (binary)
		{
			// Check if left side is identifier and right side is literal
			case { Left: IdentifierNameSyntax leftId, Right: LiteralExpressionSyntax rightLit }:
			{
				targetIdentifier = leftId.Identifier.Text;
				literal = rightLit;
				return true;
			}
			// Check if right side is identifier and left side is literal
			case { Right: IdentifierNameSyntax rightId, Left: LiteralExpressionSyntax leftLit }:
			{
				targetIdentifier = rightId.Identifier.Text;
				literal = leftLit;
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
	{
		var visitedExpression = Visit(node.Expression);

		// If the return expression is parenthesized, unwrap it to avoid returning nested parentheses that can interfere with pattern matching and other optimizations.
		if (visitedExpression is ParenthesizedExpressionSyntax parenthesized)
		{
			visitedExpression = parenthesized.Expression;
		}

		if (visitedExpression is ThrowExpressionSyntax throwExpression)
		{
			return ThrowStatement(throwExpression.Expression);
		}

		return node.WithExpression(visitedExpression as ExpressionSyntax);
	}

	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		var visited = Visit(node.Declaration);

		return visited switch
		{
			null => null,
			BlockSyntax block => block,
			VariableDeclarationSyntax declaration => node.WithDeclaration(declaration),
			ExpressionStatementSyntax expressionStatement => expressionStatement,
			ThrowStatementSyntax throwStatement => throwStatement,
			_ => node
		};
	}

	/// <summary>
	///   Replaces the first read of a named identifier with a given expression, adding
	///   parentheses only when operator precedence requires it.
	/// </summary>
	private sealed class SingleUseInliner(string varName, ExpressionSyntax initExpr) : CSharpSyntaxRewriter
	{
		private bool _replaced;

		public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
		{
			if (_replaced || node.Identifier.Text != varName || IsWriteContext(node))
			{
				return base.VisitIdentifierName(node);
			}

			_replaced = true;

			return NeedsParentheses(initExpr, node.Parent)
				? ParenthesizedExpression(initExpr)
				: initExpr;
		}

		/// <summary>
		///   Returns true when wrapping <paramref name="expr" /> in parentheses is required
		///   to preserve operator precedence in the given <paramref name="parent" /> context.
		/// </summary>
		private static bool NeedsParentheses(ExpressionSyntax expr, SyntaxNode? parent)
		{
			// Simple expressions never need parens regardless of context.
			if (expr is IdentifierNameSyntax or LiteralExpressionSyntax or InvocationExpressionSyntax
			    or MemberAccessExpressionSyntax or ElementAccessExpressionSyntax
			    or ObjectCreationExpressionSyntax or ParenthesizedExpressionSyntax)
			{
				return false;
			}

			// Safe statement/container contexts: the expression is already delimited.
			if (parent is ArgumentSyntax or ReturnStatementSyntax or EqualsValueClauseSyntax
			    or IfStatementSyntax or ArrowExpressionClauseSyntax or SwitchExpressionArmSyntax
			    or InterpolationSyntax)
			{
				return false;
			}

			return true;
		}

		private static bool IsWriteContext(IdentifierNameSyntax node)
		{
			return node.Parent switch
			{
				AssignmentExpressionSyntax { Left: var left } when left == node => true,
				PostfixUnaryExpressionSyntax { Operand: var op } when op == node => true,
				PrefixUnaryExpressionSyntax prefix when prefix.Operand == node &&
				                                        (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)) => true,
				_ => false
			};
		}
	}
}