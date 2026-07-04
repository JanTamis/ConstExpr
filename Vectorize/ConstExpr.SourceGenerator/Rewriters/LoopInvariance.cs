using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Shared loop-invariance analysis used by both <see cref="LoopInvariantCodeMotionRewriter" />
///   (which hoists invariant declarations) and <see cref="LoopUnswitchingRewriter" /> (which hoists
///   invariant branches). Kept in one place so the two loop passes cannot drift apart on what
///   "written in the loop", "loop-local", and "pure" mean.
/// </summary>
internal static class LoopInvariance
{
	/// <summary>
	///   Collects the names of all variables written anywhere inside <paramref name="scope" />
	///   (assignments, increments, declarations with mutation potential). Callers may pass either a
	///   loop body block (LICM) or the whole loop statement (unswitching, so that writes in a
	///   <c>for</c> incrementor or a side-effecting loop condition are also counted).
	/// </summary>
	public static HashSet<string> CollectWrittenInLoop(SyntaxNode scope)
	{
		var written = new HashSet<string>();

		foreach (var node in scope.DescendantNodes())
		{
			switch (node)
			{
				// x = …, x += …, x -= …, etc.
				case AssignmentExpressionSyntax { Left: IdentifierNameSyntax id }:
					written.Add(id.Identifier.Text);
					break;

				// x++, x--
				case PostfixUnaryExpressionSyntax
					{
						Operand: IdentifierNameSyntax pid
					} pue when pue.IsKind(SyntaxKind.PostIncrementExpression)
					           || pue.IsKind(SyntaxKind.PostDecrementExpression):
					written.Add(pid.Identifier.Text);
					break;

				// ++x, --x
				case PrefixUnaryExpressionSyntax
					{
						Operand: IdentifierNameSyntax preid
					} prue when prue.IsKind(SyntaxKind.PreIncrementExpression)
					            || prue.IsKind(SyntaxKind.PreDecrementExpression):
					written.Add(preid.Identifier.Text);
					break;
			}
		}

		return written;
	}

	/// <summary>
	///   Collects the names of all variables declared anywhere inside <paramref name="scope" />
	///   (local declarations, pattern/out designations). These are loop-local: their value
	///   is recomputed each iteration and is invisible outside the loop, so any expression that
	///   references one of them is not loop-invariant. Passing the whole loop statement also
	///   captures a <c>for</c>'s own iteration variable (its initializer declarator).
	/// </summary>
	public static HashSet<string> CollectLoopLocals(SyntaxNode scope)
	{
		var locals = new HashSet<string>();

		foreach (var node in scope.DescendantNodes())
		{
			switch (node)
			{
				case VariableDeclaratorSyntax declarator:
					locals.Add(declarator.Identifier.Text);
					break;

				case SingleVariableDesignationSyntax designation:
					locals.Add(designation.Identifier.Text);
					break;
			}
		}

		return locals;
	}

	/// <summary>
	///   Determines whether an expression is "pure" (no side effects): it only contains
	///   identifiers, literals, member-access chains, invocations, binary/unary expressions,
	///   casts, and element-access expressions — nothing that mutates state.
	/// </summary>
	public static bool IsPureExpression(ExpressionSyntax expr)
	{
		foreach (var node in expr.DescendantNodesAndSelf())
		{
			switch (node)
			{
				// Safe leaf and structural nodes
				case LiteralExpressionSyntax:
				case IdentifierNameSyntax:
				case MemberAccessExpressionSyntax:
				case BinaryExpressionSyntax:
				case PrefixUnaryExpressionSyntax pue when !pue.IsKind(SyntaxKind.PreIncrementExpression)
				                                          && !pue.IsKind(SyntaxKind.PreDecrementExpression):
				case PostfixUnaryExpressionSyntax poue when !poue.IsKind(SyntaxKind.PostIncrementExpression)
				                                            && !poue.IsKind(SyntaxKind.PostDecrementExpression):
				case CastExpressionSyntax:
				case ParenthesizedExpressionSyntax:
				case InvocationExpressionSyntax:
				case ArgumentListSyntax:
				case ArgumentSyntax { RefOrOutKeyword.RawKind: 0 }:
				case ElementAccessExpressionSyntax:
				case BracketedArgumentListSyntax:
				case ConditionalExpressionSyntax:
				case TypeSyntax:
					continue;

				default:
					return false;
			}
		}

		return true;
	}
}