using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for zero modulo non-zero: 0 % c = 0 (when c != 0)
/// </summary>
public class ModuloZeroStrategy : IntegerBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Left.Syntax, out var leftValue)
		    || !leftValue.IsNumericZero()
		    || !context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		    || rightValue.IsNumericZero())
			return false;
		
		optimized = SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		return true;
	}
}
