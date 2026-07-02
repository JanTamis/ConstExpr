using System.Linq;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

/// <summary>
///   Strategy for bound widening: x &lt; 10 || x &lt; 20 => x &lt; 20.
///   Mirror of ConditionalAndBoundTighteningStrategy for || instead of &&; see that class for why
///   BaseBinaryStrategy.GetChainSiblings is used instead of BinaryOptimizeContext.BinaryExpressions.
/// </summary>
public class ConditionalOrBoundWideningStrategy : BooleanBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		return TryReplaceIfRedundant(context, true, out optimized)
		       || TryReplaceIfRedundant(context, false, out optimized);
	}

	private static bool TryReplaceIfRedundant(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, bool isLeft, out ExpressionSyntax? optimized)
	{
		optimized = null;
		var operand = isLeft ? context.Left.Syntax : context.Right.Syntax;

		if (!TryGetSimpleBound(operand, context.TryGetValue, out var bound))
		{
			return false;
		}

		var isRedundant = GetChainSiblings(context, SyntaxKind.LogicalOrExpression)
			.Any(candidate => IsWiderBound(bound, candidate, context));

		if (!isRedundant)
		{
			return false;
		}

		var other = isLeft ? context.Right.Syntax : context.Left.Syntax;

		optimized = isLeft
			? LogicalOrExpression(CreateLiteral(false), other)
			: LogicalOrExpression(other, CreateLiteral(false));

		return true;
	}

	/// <summary>
	///   Whether <paramref name="candidate" /> is a same-direction comparison on the same variable that is strictly
	///   wider (less restrictive) than <paramref name="bound" />.
	/// </summary>
	private static bool IsWiderBound(SimpleBound bound, ExpressionSyntax candidate, BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context)
	{
		return TryGetSimpleBound(candidate, context.TryGetValue, out var candidateBound)
		       && candidateBound.Direction == bound.Direction
		       && LeftEqualsRight(candidateBound.Variable, bound.Variable, context.Variables)
		       && CompareBoundValues(candidateBound.Value, bound.Value) is { } cmp && cmp != 0
		       && (IsUpperBound(bound.Direction) ? cmp < 0 : cmp > 0);
	}
}