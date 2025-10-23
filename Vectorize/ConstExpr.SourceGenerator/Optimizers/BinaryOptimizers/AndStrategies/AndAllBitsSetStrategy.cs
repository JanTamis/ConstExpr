using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// x & ~0 = x and ~0 & x = x for integer types
/// </summary>
public class AndAllBitsSetStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right is { HasValue: true, Value: not null }
			&& IsAllBitsSet(context.Type.SpecialType, context.Right.Value);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}

	private static bool IsAllBitsSet(SpecialType specialType, object? value)
	{
		return specialType switch
		{
			SpecialType.System_Byte => value is byte.MaxValue,
			SpecialType.System_SByte => value is sbyte sb && unchecked((byte) sb) == byte.MaxValue,
			SpecialType.System_UInt16 => value is ushort.MaxValue,
			SpecialType.System_Int16 => value is short s && unchecked((ushort) s) == ushort.MaxValue,
			SpecialType.System_UInt32 => value is uint.MaxValue,
			SpecialType.System_Int32 => value is int i && unchecked((uint) i) == uint.MaxValue,
			SpecialType.System_UInt64 => value is ulong.MaxValue,
			SpecialType.System_Int64 => value is long l && unchecked((ulong) l) == ulong.MaxValue,
			_ => false
		};
	}
}
