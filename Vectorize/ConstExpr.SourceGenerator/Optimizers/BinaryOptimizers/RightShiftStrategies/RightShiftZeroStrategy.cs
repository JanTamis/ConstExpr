using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.RightShiftStrategies;

/// <summary>
/// Strategy for shifting zero: 0 >> x => 0 (pure)
/// </summary>
public class RightShiftZeroStrategy : IntegerBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Left.Syntax, out var leftValue)
		    || !leftValue.IsNumericZero()
		    || !IsPure(context.Right.Syntax))
			return false;
		
		optimized = SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		return true;
	}
}
