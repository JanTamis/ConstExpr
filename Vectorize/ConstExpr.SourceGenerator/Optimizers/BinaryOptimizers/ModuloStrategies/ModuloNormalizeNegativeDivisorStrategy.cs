using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for normalizing negative divisor: x % (-m) => x % m (signed integers)
/// </summary>
public class ModuloNormalizeNegativeDivisorStrategy() : IntegerBinaryStrategy<ExpressionSyntax, PrefixUnaryExpressionSyntax>(rightKind: SyntaxKind.UnaryMinusExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
    {
      return false;
    }

    optimized = BinaryExpression(SyntaxKind.ModuloExpression, context.Left.Syntax, context.Right.Syntax.Operand);
		return true;
	}
}
