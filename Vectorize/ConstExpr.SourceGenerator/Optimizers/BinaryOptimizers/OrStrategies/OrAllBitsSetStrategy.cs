using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for all-bits-set absorption: x | ~0 = ~0 (integer types)
/// </summary>
public class OrAllBitsSetStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		return (context.Right.HasValue && IsAllBitsSet(context.Right.Value, context.Type.SpecialType))
		       || (context.Left.HasValue && IsAllBitsSet(context.Left.Value, context.Type.SpecialType));
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// Return the all-bits-set operand
		if (context.Right.HasValue && IsAllBitsSet(context.Right.Value, context.Type.SpecialType))
			return context.Right.Syntax;

		if (context.Left.HasValue && IsAllBitsSet(context.Left.Value, context.Type.SpecialType))
			return context.Left.Syntax;

		return null;
	}

	private static bool IsAllBitsSet(object? value, SpecialType type)
	{
		if (value == null) return false;

		return type switch
		{
			SpecialType.System_Byte => value is byte.MaxValue,
			SpecialType.System_SByte => value is sbyte b && unchecked((byte)b) == byte.MaxValue,
			SpecialType.System_UInt16 => value is ushort.MaxValue,
			SpecialType.System_Int16 => value is short s && unchecked((ushort)s) == ushort.MaxValue,
			SpecialType.System_UInt32 => value is uint.MaxValue,
			SpecialType.System_Int32 => value is int i && unchecked((uint)i) == uint.MaxValue,
			SpecialType.System_UInt64 => value is ulong.MaxValue,
			SpecialType.System_Int64 => value is long l && unchecked((ulong)l) == ulong.MaxValue,
			_ => false
		};
	}
}
