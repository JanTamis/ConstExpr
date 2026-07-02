using System.Linq;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
///   Strategy for range contradiction: x > 10 && x < 5 => false.
///   Sourced via BaseBinaryStrategy.GetChainSiblings (connective-aware, see
///   ConditionalAndBoundTighteningStrategy for why BinaryExpressions isn't safe here).
/// </summary>
public class ConditionalAndRangeContradictionStrategy : BooleanBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		var siblings = GetChainSiblings(context, SyntaxKind.LogicalAndExpression).ToList();

		var contradicts = TryGetSimpleBound(context.Left.Syntax, context.TryGetValue, out var leftBound) && siblings.Any(c => IsContradictingBound(leftBound, c, context))
		                  || TryGetSimpleBound(context.Right.Syntax, context.TryGetValue, out var rightBound) && siblings.Any(c => IsContradictingBound(rightBound, c, context));

		if (!contradicts)
		{
			return false;
		}

		optimized = CreateLiteral(false);
		return true;
	}

	/// <summary>
	///   Whether <paramref name="candidate" /> is an opposite-direction comparison on the same variable whose range
	///   never overlaps <paramref name="bound" />.
	/// </summary>
	private static bool IsContradictingBound(SimpleBound bound, ExpressionSyntax candidate, BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context)
	{
		if (!TryGetSimpleBound(candidate, context.TryGetValue, out var candidateBound)
		    || IsUpperBound(candidateBound.Direction) == IsUpperBound(bound.Direction)
		    || !LeftEqualsRight(candidateBound.Variable, bound.Variable, context.Variables))
		{
			return false;
		}

		var (upper, lower) = IsUpperBound(bound.Direction) ? (bound, candidateBound) : (candidateBound, bound);
		var cmp = CompareBoundValues(upper.Value, lower.Value);

		return cmp switch
		{
			> 0 => true,
			0 => !(upper.Direction == SyntaxKind.GreaterThanEqualsToken && lower.Direction == SyntaxKind.LessThanEqualsToken),
			_ => false
		};
	}
}