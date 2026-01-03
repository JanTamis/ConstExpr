using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanStrategies;

/// <summary>
/// Strategy for reflexive comparison: x < x => false (pure)
/// </summary>
public class LessThanReflexiveStrategy : BaseBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context)
		    || !IsPure(context.Left.Syntax))
		{
			optimized = null;
			return false;
		}

		optimized = SyntaxHelpers.CreateLiteral(false);
		return true;
	}
}
