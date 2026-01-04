using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanOrEqualStrategies;

/// <summary>
/// Strategy for reflexive comparison: x <= x => true (pure)
/// </summary>
public class LessThanOrEqualReflexiveStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context)
		    || !IsPure(context.Left.Syntax))
		{
			optimized = null;
			return false;
		}
		
		optimized = SyntaxHelpers.CreateLiteral(true);
		return true;
	}
}
