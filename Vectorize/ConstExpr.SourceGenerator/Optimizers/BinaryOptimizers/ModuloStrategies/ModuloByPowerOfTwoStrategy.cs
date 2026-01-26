using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for power of two optimization: x % (power of two) => x & (power - 1) (unsigned integers)
/// </summary>
public class ModuloByPowerOfTwoStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Right.Syntax.IsNumericPowerOfTwo(out var power)
		    || !SyntaxHelpers.TryGetLiteral(((1 << power) - 1).ToSpecialType(context.Type.SpecialType), out var maskLiteral))
    {
      return false;
    }

    optimized = BinaryExpression(SyntaxKind.BitwiseAndExpression, context.Left.Syntax, maskLiteral);
		return true;
	}
}
