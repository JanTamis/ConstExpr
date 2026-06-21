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
	private static readonly SpecialType[] UnsignedTypes =
	[
		SpecialType.System_Byte,
		SpecialType.System_UInt16,
		SpecialType.System_UInt32,
		SpecialType.System_UInt64
	];

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
		if (type is null)
		{
			return false;
		}

		foreach (var unsignedType in UnsignedTypes)
		{
			if (type.SpecialType == unsignedType)
			{
				return true;
			}
		}

		return false;
	}
}