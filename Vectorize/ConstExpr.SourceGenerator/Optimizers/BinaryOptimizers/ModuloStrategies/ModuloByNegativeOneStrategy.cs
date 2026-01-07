using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for modulo by negative one: x % -1 = 0 (signed integers)
/// </summary>
public class ModuloByNegativeOneStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;
		
		return context.TryGetValue(context.Right.Syntax, out var rightValue)
		       && rightValue.IsNumericNegativeOne()
		       && SyntaxHelpers.TryGetLiteral(0.ToSpecialType(context.Type.SpecialType), out optimized);
	}
}