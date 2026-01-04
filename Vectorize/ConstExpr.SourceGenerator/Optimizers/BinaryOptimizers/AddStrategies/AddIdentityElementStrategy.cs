using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for identity element optimization: x + 0 = x and 0 + x = x
/// </summary>
public class AddIdentityElementStrategy : SymmetricStrategy<NumericBinaryStrategy, ExpressionSyntax, ExpressionSyntax>
{

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.TryGetLiteral(context.Left.Syntax, out var leftValue)
		    && leftValue.IsNumericZero())
		{
			optimized = context.Right.Syntax;
			return true;
		}
		
		optimized = null;
		return false;
	}
}
