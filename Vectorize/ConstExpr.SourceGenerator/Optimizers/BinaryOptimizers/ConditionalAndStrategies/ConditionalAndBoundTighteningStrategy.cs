using System.Linq;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
///   Strategy for bound tightening: x > 0 && x > 5 => x > 5.
///   Also fires when the redundant comparison sits elsewhere in the same &&-chain rather than
///   directly adjacent (e.g. x > 0 && y > 1 && x > 5), via the connective-aware chain walk in
///   BaseBinaryStrategy.GetChainSiblings — deliberately not BinaryOptimizeContext.BinaryExpressions,
///   which is operator-blind and can pull in unrelated || or outer-condition comparisons that aren't
///   actually guaranteed true here.
/// </summary>
public class ConditionalAndBoundTighteningStrategy : BooleanBinaryStrategy
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

		var isRedundant = GetChainSiblings(context, SyntaxKind.LogicalAndExpression)
			.Any(candidate => IsTighterBound(bound, candidate, context));

		if (!isRedundant)
		{
			return false;
		}

		var other = isLeft ? context.Right.Syntax : context.Left.Syntax;

		optimized = isLeft
			? LogicalAndExpression(CreateLiteral(true), other)
			: LogicalAndExpression(other, CreateLiteral(true));

		return true;
	}

	/// <summary>
	///   Whether <paramref name="candidate" /> is a same-direction comparison on the same variable that is strictly
	///   tighter than <paramref name="bound" />.
	/// </summary>
	private static bool IsTighterBound(SimpleBound bound, ExpressionSyntax candidate, BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context)
	{
		return TryGetSimpleBound(candidate, context.TryGetValue, out var candidateBound)
		       && candidateBound.Direction == bound.Direction
		       && LeftEqualsRight(candidateBound.Variable, bound.Variable, context.Variables)
		       && CompareBoundValues(candidateBound.Value, bound.Value) is { } cmp && cmp != 0
		       && (IsUpperBound(bound.Direction) ? cmp > 0 : cmp < 0);
	}
}