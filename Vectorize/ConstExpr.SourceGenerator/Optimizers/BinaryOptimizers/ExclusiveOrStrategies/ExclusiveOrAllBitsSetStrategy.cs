using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for all-bits-set: x ^ ~0 = ~x (integer types)
/// </summary>
public class ExclusiveOrAllBitsSetStrategy : IntegerBinaryStrategy
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
		// x ^ ~0 = ~x
		if (context.Right.HasValue && IsAllBitsSet(context.Right.Value, context.Type.SpecialType))
			return PrefixUnaryExpression(SyntaxKind.BitwiseNotExpression, context.Left.Syntax);

		// ~0 ^ x = ~x
		if (context.Left.HasValue && IsAllBitsSet(context.Left.Value, context.Type.SpecialType))
			return PrefixUnaryExpression(SyntaxKind.BitwiseNotExpression, context.Right.Syntax);

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
