using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanOrEqualStrategies;

/// <summary>
///   Strategy for tautological unsigned comparisons: (uint)x &gt;= 0 → true.
///   Unsigned integer types can never be negative; the comparison is always true.
///   Safe under Strict.
/// </summary>
public class GreaterThanOrEqualUnsignedZeroStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// (uint)x >= 0 → true
		if (IsUnsignedType(context.Left.Type)
		    && context.Right.Syntax.IsNumericZero())
		{
			optimized = CreateLiteral(true);
			return true;
		}

		// 0 >= (uint)x is only true when x == 0, so we do NOT optimize that here.
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