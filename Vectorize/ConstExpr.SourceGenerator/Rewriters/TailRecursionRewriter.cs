using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Tail-Recursion Elimination (TRE): rewrites a self-recursive method body
///   into an equivalent iterative <c>while (true)</c> loop.
///   Supported shapes
///   ----------------
///   <list type="bullet">
///     <item>
///       <description>
///         <b>Plain tail recursion.</b> <c>return MethodName(arg0, arg1, …);</c> — the
///         recursive call is the whole returned expression. Every recursive call in the
///         body must be in tail position.
///       </description>
///     </item>
///     <item>
///       <description>
///         <b>Accumulator recursion.</b> <c>return MethodName(…) * factor;</c> or
///         <c>return factor + MethodName(…);</c> (either operand order), mixed with
///         base-case returns, e.g. <c>if (n &lt;= 1) return 1; return Factorial(n - 1) * n;</c>.
///         The pending <c>* factor</c> / <c>+ factor</c> operations are threaded through an
///         introduced accumulator local. Only <c>+</c> and <c>*</c> are handled, and only
///         for <c>int</c> / <c>long</c> results — where reassociating the (unchecked,
///         wrapping) operation is exact. Floating-point, <c>decimal</c>, wider unsigned and
///         sub-<c>int</c> results are left unchanged.
///       </description>
///     </item>
///   </list>
///   The rewriter operates on a <see cref="MethodDeclarationSyntax" /> and replaces its
///   body with a <c>while (true) { … }</c> where every recursive call is turned into
///   parameter assignments followed by <c>continue;</c>.
///   The rewriter is conservative: if any structural invariant is not met, the original
///   body is returned unchanged.
/// </summary>
public sealed class TailRecursionRewriter
{
	/// <summary>
	///   Attempts to apply tail-recursion elimination to the given method.
	///   Returns the original body unchanged when TRE cannot be applied safely.
	///   <paramref name="returnType" /> is the method's declared result type; it is required
	///   for the accumulator shape (the introduced accumulator local is declared with it) and
	///   ignored by the plain shape. When it is <see langword="null" /> only the plain shape
	///   is attempted.
	/// </summary>
	public static BlockSyntax Apply(MethodDeclarationSyntax method, TypeSyntax? returnType = null)
	{
		var body = method.Body;

		if (body is null)
		{
			return Block();
		}

		var methodName = method.Identifier.Text;
		var paramNames = method.ParameterList.Parameters
			.Select(p => p.Identifier.Text)
			.ToList();

		if (paramNames.Count == 0)
		{
			return body;
		}

		// Shape 1 — plain tail recursion: at least one `return MethodName(args);` and every
		// recursive call already in tail position.
		if (HasTailRecursiveCall(body, methodName) && !HasNonTailRecursiveCall(body, methodName))
		{
			return ApplyPlainTailRecursion(body, methodName, paramNames);
		}

		// Shape 2 — accumulator recursion: `return MethodName(args) (+|*) factor;`. The
		// recursive call is not in tail position, so shape 1 rejected it above.
		if (TryApplyAccumulatorRecursion(body, methodName, paramNames, returnType, out var accumulated))
		{
			return accumulated;
		}

		return body;
	}

	private static BlockSyntax ApplyPlainTailRecursion(BlockSyntax body, string methodName, List<string> paramNames)
	{
		// Rewrite: replace every `return MethodName(args);` with parameter reassignments
		// + `continue`, then wrap everything in `while (true) { … }`.
		var newStatements = RewriteStatements(body.Statements, methodName, paramNames);

		if (newStatements is null)
		{
			return body;
		}

		// Flatten any top-level single-statement blocks introduced by ternary rewriting.
		var flatStatements = FlattenTopLevel(newStatements);

		// A trailing `continue` at the very end of a while(true) body is always redundant.
		while (flatStatements.Count > 0 && flatStatements[^1] is ContinueStatementSyntax)
		{
			flatStatements.RemoveAt(flatStatements.Count - 1);
		}

		var loopBody = Block(List(flatStatements));
		var whileLoop = WhileStatement(CreateLiteral(true), loopBody);

		return Block(SingletonList<StatementSyntax>(whileLoop));
	}

	// ── Detection helpers ──────────────────────────────────────────────

	/// <summary>
	///   Returns <see langword="true" /> when the block contains at least one tail-recursive
	///   call (a <c>return MethodName(…);</c> somewhere in a terminal position).
	/// </summary>
	private static bool HasTailRecursiveCall(BlockSyntax body, string methodName)
	{
		foreach (var stmt in body.Statements)
		{
			if (IsTailReturnOfMethod(stmt, methodName))
			{
				return true;
			}

			// Look inside if/else branches recursively.
			if (stmt is IfStatementSyntax ifStmt)
			{
				if (HasTailRecursiveCallInBranch(ifStmt, methodName))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool HasTailRecursiveCallInBranch(IfStatementSyntax ifStmt, string methodName)
	{
		if (IsTailReturnOfMethod(ifStmt.Statement, methodName))
		{
			return true;
		}

		if (ifStmt.Else is { } elseCl)
		{
			if (IsTailReturnOfMethod(elseCl.Statement, methodName))
			{
				return true;
			}

			if (elseCl.Statement is IfStatementSyntax nestedIf)
			{
				return HasTailRecursiveCallInBranch(nestedIf, methodName);
			}
		}

		return false;
	}

	/// <summary>
	///   Returns <see langword="true" /> if the method contains a recursive call that is NOT
	///   in tail position — e.g. used as an operand of a binary expression.
	///   Such calls cannot be eliminated by TRE.
	/// </summary>
	private static bool HasNonTailRecursiveCall(BlockSyntax body, string methodName)
	{
		foreach (var node in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			if (!IsCallToMethod(node, methodName))
			{
				continue;
			}

			// The invocation is a recursive call.  Check whether it is in tail position.
			if (!IsInTailPosition(node))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	///   Returns <see langword="true" /> when <paramref name="invocation" /> is directly
	///   returned (i.e. its parent chain ends at a <c>return</c> without intermediate
	///   binary operations, assignments, etc.).
	/// </summary>
	private static bool IsInTailPosition(InvocationExpressionSyntax invocation)
	{
		var parent = invocation.Parent;

		// Unwrap parentheses.
		while (parent is ParenthesizedExpressionSyntax)
		{
			parent = parent.Parent;
		}

		if (parent is ReturnStatementSyntax)
		{
			return true;
		}

		// Also handle: return cond ? base : Method(args)  or  return cond ? Method(args) : base
		if (parent is ConditionalExpressionSyntax conditional)
		{
			var condParent = conditional.Parent;

			while (condParent is ParenthesizedExpressionSyntax)
			{
				condParent = condParent.Parent;
			}

			return condParent is ReturnStatementSyntax;
		}

		return false;
	}

	private static bool IsTailReturnOfMethod(SyntaxNode stmt, string methodName)
	{
		if (stmt is ReturnStatementSyntax { Expression: InvocationExpressionSyntax inv })
		{
			return IsCallToMethod(inv, methodName);
		}

		// Ternary: return cond ? Method(args) : base  or  return cond ? base : Method(args)
		if (stmt is ReturnStatementSyntax { Expression: ConditionalExpressionSyntax cond })
		{
			if (cond.WhenTrue is InvocationExpressionSyntax trueInv && IsCallToMethod(trueInv, methodName))
			{
				return true;
			}

			if (cond.WhenFalse is InvocationExpressionSyntax falseInv && IsCallToMethod(falseInv, methodName))
			{
				return true;
			}
		}

		if (stmt is BlockSyntax block && block.Statements.Count > 0)
		{
			return IsTailReturnOfMethod(block.Statements.Last(), methodName);
		}

		return false;
	}

	private static bool IsCallToMethod(InvocationExpressionSyntax inv, string methodName)
	{
		return inv.Expression switch
		{
			IdentifierNameSyntax id => id.Identifier.Text == methodName,
			MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text == methodName,
			_ => false
		};
	}

	// ── Rewriting helpers ────────────────────────────────────────────

	/// <summary>
	///   Flattens one level of top-level <see cref="BlockSyntax" /> wrappers introduced by
	///   ternary rewriting so the resulting statements live directly in the while-loop body.
	/// </summary>
	private static List<StatementSyntax> FlattenTopLevel(List<StatementSyntax> statements)
	{
		var result = new List<StatementSyntax>(statements.Count);

		foreach (var stmt in statements)
		{
			if (stmt is BlockSyntax block)
			{
				result.AddRange(block.Statements);
			}
			else
			{
				result.Add(stmt);
			}
		}

		return result;
	}

	/// <summary>
	///   Rewrites a list of statements, replacing tail-recursive return statements with
	///   parameter-reassignment blocks followed by <c>continue</c>.
	///   Returns <see langword="null" /> when the transformation cannot be applied.
	/// </summary>
	private static List<StatementSyntax>? RewriteStatements(
		SyntaxList<StatementSyntax> statements,
		string methodName,
		List<string> paramNames)
	{
		var result = new List<StatementSyntax>(statements.Count);

		foreach (var stmt in statements)
		{
			var rewritten = RewriteStatement(stmt, methodName, paramNames);

			if (rewritten is null)
			{
				return null;
			}

			result.AddRange(rewritten);
		}

		return result;
	}

	/// <summary>
	///   Rewrites a single statement.  Returns <see langword="null" /> on failure,
	///   an empty list when the statement is removed, or the replacement statements.
	/// </summary>
	private static List<StatementSyntax>? RewriteStatement(
		StatementSyntax stmt,
		string methodName,
		List<string> paramNames)
	{
		// return MethodName(args); → assignments + continue
		if (stmt is ReturnStatementSyntax { Expression: InvocationExpressionSyntax inv }
		    && IsCallToMethod(inv, methodName))
		{
			var assignments = BuildParameterAssignments(inv.ArgumentList.Arguments, paramNames);

			if (assignments is null)
			{
				return null;
			}

			assignments.Add(ContinueStatement());
			return assignments;
		}

		// return cond ? base : Method(args);  or  return cond ? Method(args) : base;
		// Rewrite to: if (cond) { return base; } else { assignments; continue; }
		if (stmt is ReturnStatementSyntax { Expression: ConditionalExpressionSyntax ternary })
		{
			var ternaryRewritten = RewriteTernaryReturn(ternary, methodName, paramNames);

			if (ternaryRewritten is not null)
			{
				return [ ternaryRewritten ];
			}
		}

		// if (…) { … } else { … }  — recurse into branches
		if (stmt is IfStatementSyntax ifStmt)
		{
			var rewrittenIf = RewriteIfStatement(ifStmt, methodName, paramNames);
			return rewrittenIf is null ? null : [ rewrittenIf ];
		}

		// Block — recurse
		if (stmt is BlockSyntax block)
		{
			var inner = RewriteStatements(block.Statements, methodName, paramNames);

			if (inner is null)
			{
				return null;
			}

			return [ Block(List(inner)) ];
		}

		// Non-recursive statement — keep as-is.
		return [ stmt ];
	}

	private static IfStatementSyntax? RewriteIfStatement(
		IfStatementSyntax ifStmt,
		string methodName,
		List<string> paramNames)
	{
		var thenRewritten = RewriteStatementToBlock(ifStmt.Statement, methodName, paramNames);

		if (thenRewritten is null)
		{
			return null;
		}

		ElseClauseSyntax? elseClause = null;

		if (ifStmt.Else is { } originalElse)
		{
			StatementSyntax? elseBody = originalElse.Statement is IfStatementSyntax nestedIf
				? RewriteIfStatement(nestedIf, methodName, paramNames)
				: RewriteStatementToBlock(originalElse.Statement, methodName, paramNames);

			if (elseBody is null)
			{
				return null;
			}

			elseClause = ElseClause(elseBody);
		}

		return ifStmt
			.WithStatement(thenRewritten)
			.WithElse(elseClause);
	}

	private static BlockSyntax? RewriteStatementToBlock(
		StatementSyntax stmt,
		string methodName,
		List<string> paramNames)
	{
		if (stmt is BlockSyntax block)
		{
			var inner = RewriteStatements(block.Statements, methodName, paramNames);
			return inner is null ? null : Block(List(inner));
		}

		var single = RewriteStatement(stmt, methodName, paramNames);
		return single is null ? null : Block(List(single));
	}

	/// <summary>
	///   Rewrites a ternary tail call:
	///   <c>return cond ? base : Method(args);</c> →
	///   <c>if (cond) return base; assignments; continue;</c>
	///   or with swapped arms.
	///   Returns <see langword="null" /> when the ternary is not a tail-recursive pattern.
	/// </summary>
	private static StatementSyntax? RewriteTernaryReturn(
		ConditionalExpressionSyntax ternary,
		string methodName,
		List<string> paramNames)
	{
		// Determine which arm is the recursive call and which is the base case.
		InvocationExpressionSyntax? recursiveInv = null;
		ExpressionSyntax? baseCaseExpr = null;
		var condition = ternary.Condition;
		var recursiveIsWhenTrue = false;

		if (ternary.WhenFalse is InvocationExpressionSyntax falseInv && IsCallToMethod(falseInv, methodName))
		{
			// return cond ? base : Method(args)
			recursiveInv = falseInv;
			baseCaseExpr = ternary.WhenTrue;
		}
		else if (ternary.WhenTrue is InvocationExpressionSyntax trueInv && IsCallToMethod(trueInv, methodName))
		{
			// return cond ? Method(args) : base  → negate condition
			recursiveInv = trueInv;
			baseCaseExpr = ternary.WhenFalse;
			recursiveIsWhenTrue = true;
		}

		if (recursiveInv is null || baseCaseExpr is null)
		{
			return null;
		}

		var assignments = BuildParameterAssignments(recursiveInv.ArgumentList.Arguments, paramNames);

		if (assignments is null)
		{
			return null;
		}

		assignments.Add(ContinueStatement());

		// Build: if (baseCondition) { return base; } assignments + continue
		// baseCondition is `cond` when recursive is WhenFalse, `!cond` when recursive is WhenTrue.
		var baseCondition = recursiveIsWhenTrue
			? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(condition))
			: condition;

		var baseReturn = ReturnStatement(baseCaseExpr);
		var ifBase = IfStatement(baseCondition, Block(SingletonList<StatementSyntax>(baseReturn)));

		var result = new List<StatementSyntax>(assignments.Count + 1) { ifBase };
		result.AddRange(assignments);

		return Block(List(result));
	}

	/// <summary>
	///   Builds a list of assignment statements that update each parameter to its
	///   new value from the recursive call's argument list.
	///   Uses temporary variables when an argument references a parameter that is also
	///   being updated (to avoid read-before-write ordering issues).
	/// </summary>
	private static List<StatementSyntax>? BuildParameterAssignments(
		SeparatedSyntaxList<ArgumentSyntax> args,
		List<string> paramNames)
	{
		if (args.Count != paramNames.Count)
		{
			return null;
		}

		var result = new List<StatementSyntax>(paramNames.Count * 2);

		// Detect whether any argument expression references a parameter that will be
		// overwritten by an earlier assignment (aliasing / ordering hazard).
		// If so, capture all arguments into temporaries first.
		var needsTemporaries = false;

		for (var i = 0; i < args.Count; i++)
		{
			var argIdentifiers = new HashSet<string>(
				args[i].Expression
					.DescendantNodesAndSelf()
					.OfType<IdentifierNameSyntax>()
					.Select(id => id.Identifier.Text));

			// Check if any later-assigned parameter appears in this expression.
			for (var j = 0; j < i; j++)
			{
				if (argIdentifiers.Contains(paramNames[j]))
				{
					needsTemporaries = true;
					break;
				}
			}

			if (needsTemporaries)
			{
				break;
			}
		}

		if (needsTemporaries)
		{
			// Phase 1: capture all arguments into temporaries.
			var tempNames = new List<string>(paramNames.Count);

			for (var i = 0; i < paramNames.Count; i++)
			{
				var tmpName = $"_tre_tmp_{paramNames[i]}";
				tempNames.Add(tmpName);

				result.Add(LocalDeclarationStatement(
					VariableDeclaration(IdentifierName("var"))
						.WithVariables(SeparatedList(
						[
							VariableDeclarator(Identifier(tmpName))
								.WithInitializer(EqualsValueClause(args[i].Expression))
						]))));
			}

			// Phase 2: assign temporaries to parameters.
			for (var i = 0; i < paramNames.Count; i++)
			{
				result.Add(ExpressionStatement(
					AssignmentExpression(IdentifierName(paramNames[i]), IdentifierName(tempNames[i]))));
			}
		}
		else
		{
			for (var i = 0; i < paramNames.Count; i++)
			{
				result.Add(ExpressionStatement(
					AssignmentExpression(IdentifierName(paramNames[i]), args[i].Expression)));
			}
		}

		return result;
	}

	// ── Accumulator recursion ────────────────────────────────────────

	/// <summary>
	///   Rewrites <c>return MethodName(args) (+|*) factor;</c> style recursion (mixed with
	///   base-case returns) into a loop that threads the pending <c>+</c>/<c>*</c> operations
	///   through an introduced accumulator local.
	/// </summary>
	private static bool TryApplyAccumulatorRecursion(
		BlockSyntax body,
		string methodName,
		List<string> paramNames,
		TypeSyntax? returnType,
		[NotNullWhen(true)] out BlockSyntax? result)
	{
		result = null;

		// v1 scope: only `int` / `long` results. For those, reassociating the wrapping
		// (unchecked) `+` / `*` accumulator is exact. Wider unsigned, sub-`int`, floating and
		// `decimal` results each need a cast on assignment or an associativity opt-in.
		if (returnType is null || !IsIntOrLong(returnType))
		{
			return false;
		}

		// An explicit checked/unchecked region could make the reassociation observable.
		if (body.DescendantNodes().Any(n => n is CheckedExpressionSyntax or CheckedStatementSyntax))
		{
			return false;
		}

		var recursiveCalls = body.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Where(inv => IsCallToMethod(inv, methodName))
			.ToList();

		if (recursiveCalls.Count == 0)
		{
			return false;
		}

		// Every recursive call must be `MethodName(args) op factor` returned directly (or as one
		// arm of a `return cond ? … : …;`), the factor free of recursion, and all sites must
		// share the same operator and the full parameter arity.
		var accumulatorOperator = SyntaxKind.None;

		foreach (var call in recursiveCalls)
		{
			if (!TryGetAccumulatorContext(call, methodName, out var op))
			{
				return false;
			}

			if (accumulatorOperator == SyntaxKind.None)
			{
				accumulatorOperator = op;
			}
			else if (accumulatorOperator != op)
			{
				return false;
			}

			if (call.ArgumentList.Arguments.Count != paramNames.Count)
			{
				return false;
			}
		}

		var accumulatorName = MakeAccumulatorName(body);

		var rewritten = RewriteAccumulatorStatements(body.Statements, methodName, paramNames, accumulatorOperator, accumulatorName);

		if (rewritten is null)
		{
			return false;
		}

		var flatStatements = FlattenTopLevel(rewritten);

		while (flatStatements.Count > 0 && flatStatements[^1] is ContinueStatementSyntax)
		{
			flatStatements.RemoveAt(flatStatements.Count - 1);
		}

		var accumulatorDeclaration = LocalDeclarationStatement(
			VariableDeclaration(returnType.WithoutTrivia())
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(accumulatorName))
						.WithInitializer(EqualsValueClause(IdentityLiteral(accumulatorOperator, returnType))))));

		var whileLoop = WhileStatement(CreateLiteral(true), Block(List(flatStatements)));

		result = Block(accumulatorDeclaration, whileLoop);
		return true;
	}

	/// <summary>
	///   Verifies that <paramref name="call" /> sits in accumulator position: a direct operand
	///   of a <c>+</c> / <c>*</c> whose other operand carries no recursion, and that binary is
	///   the whole returned expression (directly, or as one arm of a ternary return).
	/// </summary>
	private static bool TryGetAccumulatorContext(InvocationExpressionSyntax call, string methodName, out SyntaxKind accumulatorOperator)
	{
		accumulatorOperator = SyntaxKind.None;

		var parent = call.Parent;

		while (parent is ParenthesizedExpressionSyntax paren)
		{
			parent = paren.Parent;
		}

		if (parent is not BinaryExpressionSyntax binary
		    || !TryMatchAccumulatorBinary(binary, methodName, out var matchedCall, out _)
		    || matchedCall != call)
		{
			return false;
		}

		var binaryParent = binary.Parent;

		while (binaryParent is ParenthesizedExpressionSyntax paren)
		{
			binaryParent = paren.Parent;
		}

		if (binaryParent is ReturnStatementSyntax)
		{
			accumulatorOperator = binary.Kind();
			return true;
		}

		if (binaryParent is ConditionalExpressionSyntax conditional
		    && (Unparenthesize(conditional.WhenTrue) == binary || Unparenthesize(conditional.WhenFalse) == binary))
		{
			var conditionalParent = conditional.Parent;

			while (conditionalParent is ParenthesizedExpressionSyntax paren)
			{
				conditionalParent = paren.Parent;
			}

			if (conditionalParent is ReturnStatementSyntax)
			{
				accumulatorOperator = binary.Kind();
				return true;
			}
		}

		return false;
	}

	private static bool TryMatchAccumulatorBinary(
		BinaryExpressionSyntax binary,
		string methodName,
		[NotNullWhen(true)] out InvocationExpressionSyntax? recursiveCall,
		[NotNullWhen(true)] out ExpressionSyntax? factor)
	{
		recursiveCall = null;
		factor = null;

		if (!binary.IsKind(SyntaxKind.AddExpression) && !binary.IsKind(SyntaxKind.MultiplyExpression))
		{
			return false;
		}

		var left = Unparenthesize(binary.Left);
		var right = Unparenthesize(binary.Right);

		if (left is InvocationExpressionSyntax leftCall
		    && IsCallToMethod(leftCall, methodName)
		    && !ContainsRecursiveCall(binary.Right, methodName))
		{
			recursiveCall = leftCall;
			factor = binary.Right;
			return true;
		}

		if (right is InvocationExpressionSyntax rightCall
		    && IsCallToMethod(rightCall, methodName)
		    && !ContainsRecursiveCall(binary.Left, methodName))
		{
			recursiveCall = rightCall;
			factor = binary.Left;
			return true;
		}

		return false;
	}

	private static List<StatementSyntax>? RewriteAccumulatorStatements(
		SyntaxList<StatementSyntax> statements,
		string methodName,
		List<string> paramNames,
		SyntaxKind accumulatorOperator,
		string accumulatorName)
	{
		var result = new List<StatementSyntax>(statements.Count);

		foreach (var stmt in statements)
		{
			var rewritten = RewriteAccumulatorStatement(stmt, methodName, paramNames, accumulatorOperator, accumulatorName);

			if (rewritten is null)
			{
				return null;
			}

			result.AddRange(rewritten);
		}

		return result;
	}

	private static List<StatementSyntax>? RewriteAccumulatorStatement(
		StatementSyntax stmt,
		string methodName,
		List<string> paramNames,
		SyntaxKind accumulatorOperator,
		string accumulatorName)
	{
		switch (stmt)
		{
			case ReturnStatementSyntax { Expression: { } expression }:
			{
				var inner = Unparenthesize(expression);

				// return MethodName(args) op factor;
				if (inner is BinaryExpressionSyntax binary
				    && TryMatchAccumulatorBinary(binary, methodName, out var recursiveCall, out var factor))
				{
					return BuildAccumulatorStep(recursiveCall, factor, paramNames, accumulatorOperator, accumulatorName);
				}

				// return cond ? <base> : MethodName(args) op factor;   (either arm order)
				if (inner is ConditionalExpressionSyntax conditional)
				{
					var rewrittenTernary = RewriteAccumulatorTernary(conditional, methodName, paramNames, accumulatorOperator, accumulatorName);

					if (rewrittenTernary is not null)
					{
						return [ rewrittenTernary ];
					}
				}

				// Base case: a return with no recursive call — fold the pending accumulator in.
				if (!ContainsRecursiveCall(expression, methodName))
				{
					return [ ReturnStatement(FoldAccumulatorIntoBase(expression, accumulatorOperator, accumulatorName)) ];
				}

				return null;
			}
			case IfStatementSyntax ifStmt:
			{
				var rewrittenIf = RewriteAccumulatorIfStatement(ifStmt, methodName, paramNames, accumulatorOperator, accumulatorName);
				return rewrittenIf is null ? null : [ rewrittenIf ];
			}
			case BlockSyntax block:
			{
				var inner = RewriteAccumulatorStatements(block.Statements, methodName, paramNames, accumulatorOperator, accumulatorName);
				return inner is null ? null : [ Block(List(inner)) ];
			}
			default:
			{
				// Keep only statements we fully understand. A `return` we did not rewrite — one
				// nested in a loop / switch / try / using we pass through untouched — would escape
				// with the bare base value while the accumulator still holds pending factors, and
				// the surrounding `while (true)` would loop on it. Bail rather than miscompile.
				return ContainsRecursiveCall(stmt, methodName) || ContainsOwnReturn(stmt)
					? null
					: [ stmt ];
			}
		}
	}

	private static List<StatementSyntax>? BuildAccumulatorStep(
		InvocationExpressionSyntax recursiveCall,
		ExpressionSyntax factor,
		List<string> paramNames,
		SyntaxKind accumulatorOperator,
		string accumulatorName)
	{
		var assignments = BuildParameterAssignments(recursiveCall.ArgumentList.Arguments, paramNames);

		if (assignments is null)
		{
			return null;
		}

		var step = new List<StatementSyntax>(assignments.Count + 2)
		{
			// acc = acc <op> (factor);  — evaluated with the current parameter values, before
			// they are reassigned for the next iteration.
			ExpressionStatement(AssignmentExpression(
				IdentifierName(accumulatorName),
				BinaryExpression(accumulatorOperator, IdentifierName(accumulatorName), MaybeParenthesize(factor))))
		};

		step.AddRange(assignments);
		step.Add(ContinueStatement());
		return step;
	}

	private static StatementSyntax? RewriteAccumulatorTernary(
		ConditionalExpressionSyntax ternary,
		string methodName,
		List<string> paramNames,
		SyntaxKind accumulatorOperator,
		string accumulatorName)
	{
		var whenTrue = Unparenthesize(ternary.WhenTrue);
		var whenFalse = Unparenthesize(ternary.WhenFalse);

		InvocationExpressionSyntax? recursiveCall = null;
		ExpressionSyntax? factor = null;
		ExpressionSyntax? baseExpression = null;
		var recursiveIsWhenTrue = false;

		if (whenFalse is BinaryExpressionSyntax falseBinary
		    && TryMatchAccumulatorBinary(falseBinary, methodName, out recursiveCall, out factor))
		{
			baseExpression = ternary.WhenTrue;
		}
		else if (whenTrue is BinaryExpressionSyntax trueBinary
		         && TryMatchAccumulatorBinary(trueBinary, methodName, out recursiveCall, out factor))
		{
			baseExpression = ternary.WhenFalse;
			recursiveIsWhenTrue = true;
		}

		if (recursiveCall is null || factor is null || baseExpression is null || ContainsRecursiveCall(baseExpression, methodName))
		{
			return null;
		}

		var step = BuildAccumulatorStep(recursiveCall, factor, paramNames, accumulatorOperator, accumulatorName);

		if (step is null)
		{
			return null;
		}

		var baseCondition = recursiveIsWhenTrue
			? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(ternary.Condition))
			: ternary.Condition;

		var baseReturn = ReturnStatement(FoldAccumulatorIntoBase(baseExpression, accumulatorOperator, accumulatorName));
		var ifBase = IfStatement(baseCondition, Block(SingletonList<StatementSyntax>(baseReturn)));

		var statements = new List<StatementSyntax>(step.Count + 1) { ifBase };
		statements.AddRange(step);

		return Block(List(statements));
	}

	private static IfStatementSyntax? RewriteAccumulatorIfStatement(
		IfStatementSyntax ifStmt,
		string methodName,
		List<string> paramNames,
		SyntaxKind accumulatorOperator,
		string accumulatorName)
	{
		var thenRewritten = RewriteAccumulatorStatementToBlock(ifStmt.Statement, methodName, paramNames, accumulatorOperator, accumulatorName);

		if (thenRewritten is null)
		{
			return null;
		}

		ElseClauseSyntax? elseClause = null;

		if (ifStmt.Else is { } originalElse)
		{
			StatementSyntax? elseBody = originalElse.Statement is IfStatementSyntax nestedIf
				? RewriteAccumulatorIfStatement(nestedIf, methodName, paramNames, accumulatorOperator, accumulatorName)
				: RewriteAccumulatorStatementToBlock(originalElse.Statement, methodName, paramNames, accumulatorOperator, accumulatorName);

			if (elseBody is null)
			{
				return null;
			}

			elseClause = ElseClause(elseBody);
		}

		return ifStmt.WithStatement(thenRewritten).WithElse(elseClause);
	}

	private static BlockSyntax? RewriteAccumulatorStatementToBlock(
		StatementSyntax stmt,
		string methodName,
		List<string> paramNames,
		SyntaxKind accumulatorOperator,
		string accumulatorName)
	{
		if (stmt is BlockSyntax block)
		{
			var inner = RewriteAccumulatorStatements(block.Statements, methodName, paramNames, accumulatorOperator, accumulatorName);
			return inner is null ? null : Block(List(inner));
		}

		var single = RewriteAccumulatorStatement(stmt, methodName, paramNames, accumulatorOperator, accumulatorName);
		return single is null ? null : Block(List(single));
	}

	private static ExpressionSyntax FoldAccumulatorIntoBase(ExpressionSyntax baseExpression, SyntaxKind accumulatorOperator, string accumulatorName)
	{
		// acc <op> identity  ==  acc, so a bare identity base collapses to just the accumulator.
		if (IsIdentityLiteral(Unparenthesize(baseExpression), accumulatorOperator))
		{
			return IdentifierName(accumulatorName);
		}

		return BinaryExpression(accumulatorOperator, IdentifierName(accumulatorName), MaybeParenthesize(baseExpression));
	}

	private static ExpressionSyntax IdentityLiteral(SyntaxKind accumulatorOperator, TypeSyntax returnType)
	{
		var value = accumulatorOperator == SyntaxKind.MultiplyExpression ? 1 : 0;

		// The pipeline may rewrite `long acc = 1;` to `var acc = 1;`, which would silently make the
		// accumulator `int` — so the literal itself has to carry the width (`1L`, not `1`).
		var isLong = returnType switch
		{
			PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.LongKeyword),
			IdentifierNameSyntax { Identifier.Text: "Int64" } => true,
			QualifiedNameSyntax { Right.Identifier.Text: "Int64" } => true,
			_ => false
		};

		return isLong ? CreateLiteral((long) value) : CreateLiteral(value);
	}

	private static bool IsIdentityLiteral(ExpressionSyntax expression, SyntaxKind accumulatorOperator)
	{
		if (expression is not LiteralExpressionSyntax literal || literal.Token.Value is not { } value)
		{
			return false;
		}

		try
		{
			var number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
			return accumulatorOperator == SyntaxKind.MultiplyExpression ? number == 1 : number == 0;
		}
		catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
		{
			return false;
		}
	}

	private static ExpressionSyntax MaybeParenthesize(ExpressionSyntax expression)
	{
		return expression switch
		{
			IdentifierNameSyntax or LiteralExpressionSyntax or InvocationExpressionSyntax
				or MemberAccessExpressionSyntax or ElementAccessExpressionSyntax
				or ParenthesizedExpressionSyntax => expression,
			_ => ParenthesizedExpression(expression)
		};
	}

	private static bool IsIntOrLong(TypeSyntax type)
	{
		return type switch
		{
			PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.IntKeyword)
			                                   || predefined.Keyword.IsKind(SyntaxKind.LongKeyword),
			IdentifierNameSyntax { Identifier.Text: "Int32" or "Int64" } => true,
			QualifiedNameSyntax { Right.Identifier.Text: "Int32" or "Int64" } => true,
			_ => false
		};
	}

	private static bool ContainsRecursiveCall(SyntaxNode node, string methodName)
	{
		return node.DescendantNodesAndSelf()
			.OfType<InvocationExpressionSyntax>()
			.Any(inv => IsCallToMethod(inv, methodName));
	}

	/// <summary>
	///   True when <paramref name="node" /> contains a <c>return</c> that belongs to the method
	///   itself — a <c>return</c> inside a nested local function or lambda does not count.
	/// </summary>
	private static bool ContainsOwnReturn(SyntaxNode node)
	{
		return node.DescendantNodesAndSelf()
			.OfType<ReturnStatementSyntax>()
			.Any(returnStatement =>
			{
				foreach (var ancestor in returnStatement.Ancestors())
				{
					if (ancestor == node)
					{
						return true;
					}

					if (ancestor is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
					{
						return false;
					}
				}

				return true;
			});
	}

	private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax paren)
		{
			expression = paren.Expression;
		}

		return expression;
	}

	private static string MakeAccumulatorName(BlockSyntax body)
	{
		var used = new HashSet<string>(
			body.DescendantTokens()
				.Where(t => t.IsKind(SyntaxKind.IdentifierToken))
				.Select(t => t.Text));

		const string baseName = "treAcc";

		if (!used.Contains(baseName))
		{
			return baseName;
		}

		for (var i = 0;; i++)
		{
			var candidate = baseName + i;

			if (!used.Contains(candidate))
			{
				return candidate;
			}
		}
	}
}