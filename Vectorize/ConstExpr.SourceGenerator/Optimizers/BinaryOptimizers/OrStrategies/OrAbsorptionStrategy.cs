using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for absorption law: x | (x & y) = x and (x & y) | x = x (pure)
/// </summary>
public class OrAbsorptionStrategy : SymmetricStrategy<NumericOrBooleanBinaryStrategy, ExpressionSyntax, BinaryExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// x | (x & y) = x
		if (LeftEqualsRight(context.Left.Syntax, context.Right.Syntax.Left, context.Variables) 
		    || LeftEqualsRight(context.Left.Syntax, context.Right.Syntax.Right, context.Variables))
		{
			optimized = context.Left.Syntax;
			return true;
		}
		
		optimized = null;
		return false;
	}
}
