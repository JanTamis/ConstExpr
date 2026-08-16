using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Flattens a chain of <c>+</c>/<c>-</c> operators, collapses structurally-identical terms into
///   one scaled term, and sums the chain's literal operands into a single trailing constant:
///   <c>x + 10 + x - 5</c> becomes <c>(x &lt;&lt; 1) + 5</c>.
///   <para>
///     Only fires when every term resolves to an integer type and none can have a side effect (no
///     calls, indexers, object creation, or increment/decrement) — for those, reassociation is
///     always exact, since two's-complement addition/subtraction is associative and commutative
///     regardless of overflow. Floating-point and <c>decimal</c> chains are left untouched:
///     reordering their arithmetic can change rounding, which is
///     <see cref="ConstExpr.Core.Enumerators.FastMathFlags.AssociativeMath" />'s call, not this pass's.
///   </para>
/// </summary>
public sealed class ReassociationRewriter(SemanticModel semanticModel, ConcurrentDictionary<ulong, ISymbol> symbolStore) : CSharpSyntaxRewriter
{
	public static SyntaxNode Apply(SyntaxNode node, SemanticModel semanticModel, ConcurrentDictionary<ulong, ISymbol> symbolStore)
	{
		return new ReassociationRewriter(semanticModel, symbolStore).Visit(node);
	}

	public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		var visited = (BinaryExpressionSyntax) base.VisitBinaryExpression(node)!;

		// Only the outermost node of a chain reassociates: an inner link's original parent being
		// another Add/Subtract means the chain continues upward, and that ancestor call flattens
		// (and can see) this whole subtree in one pass.
		if (visited.Kind() is not (SyntaxKind.AddExpression or SyntaxKind.SubtractExpression)
		    || node.Parent is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.AddExpression or (int) SyntaxKind.SubtractExpression })
		{
			return visited;
		}

		var terms = new List<(ExpressionSyntax Expr, bool Negative)>();
		Flatten(visited, false, terms);

		if (terms.Count < 2 || terms.Any(t => HasPotentialSideEffect(t.Expr)))
		{
			return visited;
		}

		ITypeSymbol? integerType = null;

		foreach (var term in terms)
		{
			if (term.Expr is LiteralExpressionSyntax)
			{
				continue;
			}

			if (semanticModel.TryGetTypeSymbol(term.Expr, symbolStore, out var type))
			{
				if (!type.IsNumericType() || type.IsFloatingNumeric())
				{
					// Provably not a safe integer term (float/double/decimal, or non-numeric) —
					// bail rather than risk reassociating something where order can matter.
					return visited;
				}

				integerType ??= type;
				continue;
			}

			// Type didn't resolve — common for a node an earlier pass synthesized without a type
			// annotation (e.g. a strength-reduction `x << 1`), which the semantic model can't bind
			// since it was never part of the compiled tree. Shift and bitwise operators are only
			// ever defined on integer operands in C#, so this is still provably safe unresolved;
			// anything else with an unresolved type is not, and bails.
			if (term.Expr is not BinaryExpressionSyntax
			    {
				    RawKind: (int) SyntaxKind.LeftShiftExpression or (int) SyntaxKind.RightShiftExpression or (int) SyntaxKind.UnsignedRightShiftExpression
				    or (int) SyntaxKind.BitwiseAndExpression or (int) SyntaxKind.BitwiseOrExpression or (int) SyntaxKind.ExclusiveOrExpression
			    })
			{
				return visited;
			}
		}

		// Every term checked out, but none resolved to a concrete type to build replacement
		// literals against (e.g. a chain of nothing but unresolved shifts) — bail.
		if (integerType is null)
		{
			return visited;
		}

		var zero = 0.ToSpecialType(integerType.SpecialType);
		var groups = new List<(ExpressionSyntax Expr, int Coefficient)>();
		var groupIndex = new Dictionary<ExpressionSyntax, int>(SyntaxNodeComparer.Get<ExpressionSyntax>());
		var literalSum = zero;
		var nonLiteralCount = 0;

		foreach (var (expr, negative) in terms)
		{
			if (expr is LiteralExpressionSyntax { Token.Value: { } literalValue })
			{
				literalSum = ObjectExtensions.ExecuteBinaryOperation(negative ? SyntaxKind.SubtractExpression : SyntaxKind.AddExpression, literalSum, literalValue);
				continue;
			}

			nonLiteralCount++;

			if (groupIndex.TryGetValue(expr, out var index))
			{
				groups[index] = (groups[index].Expr, groups[index].Coefficient + (negative ? -1 : 1));
			}
			else
			{
				groupIndex[expr] = groups.Count;
				groups.Add((expr, negative ? -1 : 1));
			}
		}

		var literalIsZero = Equals(literalSum, zero);
		var literalTermCount = terms.Count(t => t.Expr is LiteralExpressionSyntax);

		// Nothing to combine: no term repeated (every group has coefficient +-1) and at most one
		// literal to fold — reassociating would just rebuild the same shape with new node
		// identities, churning the tree for no benefit.
		var changed = groups.Count < nonLiteralCount || groups.Any(g => g.Coefficient is not (1 or -1)) || literalTermCount > 1;

		if (!changed)
		{
			return visited;
		}

		var outputTerms = new List<(ExpressionSyntax Magnitude, bool Negative)>();

		foreach (var (expr, coefficient) in groups)
		{
			if (coefficient == 0)
			{
				continue;
			}

			outputTerms.Add((Scale(expr, Math.Abs(coefficient), integerType), coefficient < 0));
		}

		if (!literalIsZero)
		{
			var isNegative = ObjectExtensions.ExecuteBinaryOperation(SyntaxKind.LessThanExpression, literalSum, zero) is true;
			var magnitude = isNegative ? literalSum.Abs(integerType.SpecialType) : literalSum;
			outputTerms.Add((CreateLiteral(magnitude), isNegative));
		}

		if (outputTerms.Count == 0)
		{
			return CreateLiteral(zero);
		}

		// Subtracting from a leading zero (rather than a unary minus) sidesteps two pitfalls: unary
		// minus isn't defined for uint/ulong, and it needs its own precedence-safety check that a
		// binary subtract, reusing the wrapping every other term already went through, does not.
		var result = outputTerms[0].Negative
			? SubtractExpression(CreateLiteral(zero), outputTerms[0].Magnitude)
			: outputTerms[0].Magnitude;

		for (var i = 1; i < outputTerms.Count; i++)
		{
			var (magnitude, negative) = outputTerms[i];
			result = negative ? SubtractExpression(result, magnitude) : AddExpression(result, magnitude);
		}

		return result;
	}

	private static void Flatten(ExpressionSyntax expr, bool negate, List<(ExpressionSyntax, bool)> terms)
	{
		switch (expr)
		{
			case ParenthesizedExpressionSyntax paren:
			{
				Flatten(paren.Expression, negate, terms);
				return;
			}
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.AddExpression } add:
			{
				Flatten(add.Left, negate, terms);
				Flatten(add.Right, negate, terms);
				return;
			}
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.SubtractExpression } subtract:
			{
				Flatten(subtract.Left, negate, terms);
				Flatten(subtract.Right, !negate, terms);
				return;
			}
			default:
			{
				terms.Add((expr.WithoutTrivia(), negate));
				return;
			}
		}
	}

	private static bool HasPotentialSideEffect(ExpressionSyntax expr)
	{
		return expr.DescendantNodesAndSelf().Any(n => n is InvocationExpressionSyntax
			or ElementAccessExpressionSyntax
			or ObjectCreationExpressionSyntax
			or ImplicitObjectCreationExpressionSyntax
			or AssignmentExpressionSyntax
			or PostfixUnaryExpressionSyntax
			or PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.PreIncrementExpression or (int) SyntaxKind.PreDecrementExpression });
	}

	/// <summary>
	///   Builds <paramref name="expr" /> scaled by <paramref name="count" />, as a shift when that's a power of two,
	///   otherwise a multiply.
	/// </summary>
	private static ExpressionSyntax Scale(ExpressionSyntax expr, int count, ITypeSymbol type)
	{
		if (count == 1)
		{
			return Wrap(expr);
		}

		if ((count & count - 1) == 0)
		{
			var shift = 0;

			while (1 << shift < count)
			{
				shift++;
			}

			return ParenthesizedExpression(LeftShiftExpression(Wrap(expr), CreateLiteral(shift)));
		}

		return MultiplyExpression(Wrap(expr), CreateLiteral(count.ToSpecialType(type.SpecialType)));
	}

	/// <summary>
	///   Parenthesizes <paramref name="expr" /> when needed to safely appear as an operand of
	///   <c>+</c>, <c>-</c>, <c>*</c>, or <c>&lt;&lt;</c> — every kind this pass ever embeds a term
	///   into. Additive and multiplicative expressions all bind tighter than shift, so the same
	///   "already safe" set works for all four positions.
	/// </summary>
	private static ExpressionSyntax Wrap(ExpressionSyntax expr)
	{
		return expr switch
		{
			LiteralExpressionSyntax or IdentifierNameSyntax or MemberAccessExpressionSyntax
				or InvocationExpressionSyntax or ElementAccessExpressionSyntax
				or ParenthesizedExpressionSyntax or CastExpressionSyntax
				or PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => expr,
			BinaryExpressionSyntax binary when binary.Kind() is
				SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression
				or SyntaxKind.AddExpression or SyntaxKind.SubtractExpression => expr,
			_ => ParenthesizedExpression(expr)
		};
	}
}