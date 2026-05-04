using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanStrategies;

/// <summary>
/// Strategy for tautological unsigned comparisons: (uint)x &lt; 0 → false.
/// Unsigned integer types can never be negative; the comparison is always false.
/// Safe under Strict.
/// </summary>
public class LessThanUnsignedZeroStrategy : BaseBinaryStrategy
{
	private static readonly SpecialType[] UnsignedTypes =
	[
		SpecialType.System_Byte,
		SpecialType.System_UInt16,
		SpecialType.System_UInt32,
		SpecialType.System_UInt64,
	];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// (uint)x < 0 → false
		if (IsUnsignedType(context.Left.Type)
		    && context.Right.Syntax.IsNumericZero())
		{
			optimized = CreateLiteral(false);
			return true;
		}

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
