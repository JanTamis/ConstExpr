using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanStrategies;

/// <summary>
///   Strategy for unsigned zero comparisons: (uint)x &gt; 0 → x != 0.
///   Unsigned integer types can never be negative, so "greater than zero" is exactly
///   "not zero" — one comparison instead of a range check. Safe under Strict.
/// </summary>
public class GreaterThanUnsignedZeroStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// (uint)x > 0 → x != 0
		if (IsUnsignedType(context.Left.Type)
		    && context.Right.Syntax.IsNumericZero())
		{
			optimized = NotEqualsExpression(context.Left.Syntax, context.Right.Syntax);
			return true;
		}

		// 0 > (uint)x is always false, but GreaterThanReverseStrategy already normalizes
		// that to x < 0, which LessThanUnsignedZeroStrategy folds to false.
		optimized = null;
		return false;
	}

	private static bool IsUnsignedType(ITypeSymbol? type)
	{
		return type?.SpecialType is SpecialType.System_Byte
			or SpecialType.System_UInt16
			or SpecialType.System_UInt32
			or SpecialType.System_UInt64;
	}
}
