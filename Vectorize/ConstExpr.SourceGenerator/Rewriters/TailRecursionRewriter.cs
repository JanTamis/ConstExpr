using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Tail-Recursion Elimination (TRE): rewrites a method body that contains
///   only tail-recursive calls into an equivalent iterative <c>while (true)</c> loop.
///   A recursive call at position <em>P</em> is a tail call when <em>P</em> is the
///   last operation before the method returns, i.e. the call result is immediately
///   returned without any further processing.
///   Supported shapes
///   ----------------
///   <list type="bullet">
///     <item>
///       <description>
///         <c>return MethodName(arg0, arg1, …);</c> — direct unconditional tail call.
///       </description>
///     </item>
///     <item>
///       <description>
///         Conditional tail calls mixed with base-case returns, e.g.
///         <c>if (n &lt;= 1) return 1; return Factorial(n - 1) * n;</c> — NOT supported
///         (the multiplication makes the last call non-tail). Only when the final
///         <c>return</c> is a bare <c>return MethodName(…);</c> is TRE applied.
///       </description>
///     </item>
///   </list>
///   The rewriter operates on a <see cref="MethodDeclarationSyntax" /> and replaces its
///   body with a <c>while (true) { … }</c> where every tail call is turned into
///   parameter assignments followed by <c>continue;</c>.
///   The rewriter is conservative: if any structural invariant is not met, the original
///   body is returned unchanged.
/// </summary>
public sealed class TailRecursionRewriter
{
	/// <summary>
	///   Attempts to apply tail-recursion elimination to the given method.
	///   Returns the original body unchanged when TRE cannot be applied safely.
	/// </summary>
	public static BlockSyntax Apply(MethodDeclarationSyntax method)
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

		// Verify the method has at least one tail-recursive call and that ALL recursive
		// calls are in tail position (no recursive call in non-tail position).
		if (!HasTailRecursiveCall(body, methodName))
		{
			return body;
		}

		if (HasNonTailRecursiveCall(body, methodName))
		{
			return body;
		}

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
}