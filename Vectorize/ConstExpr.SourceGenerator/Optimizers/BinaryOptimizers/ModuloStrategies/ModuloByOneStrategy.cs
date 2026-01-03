using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for modulo by one: x % 1 = 0
/// </summary>
public class ModuloByOneStrategy : IntegerBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		return context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		       && rightValue.IsNumericOne()
		       && SyntaxHelpers.TryGetLiteral(0.ToSpecialType(context.Type.SpecialType), out optimized);
	}
}
