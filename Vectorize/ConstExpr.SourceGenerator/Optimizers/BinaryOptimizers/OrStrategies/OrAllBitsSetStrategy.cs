using System;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
///   Strategy for all-bits-set absorption: x | ~0 = ~0 (integer types)
/// </summary>
public class OrAllBitsSetStrategy : SymmetricStrategy<IntegerBinaryStrategy, ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.TryGetValue(context.Right.Syntax, out var rightValue)
		    || !IsAllBitsSet(rightValue, context.Type.SpecialType))
		{
			optimized = null;
			return false;
		}

		optimized = context.Right.Syntax;
		return true;
	}

	private static bool IsAllBitsSet(object? value, SpecialType type)
	{
		if (value == null)
		{
			return false;
		}

		return type switch
		{
			SpecialType.System_Byte => value is Byte.MaxValue,
			SpecialType.System_SByte => value is sbyte b && unchecked((byte)b) == Byte.MaxValue,
			SpecialType.System_UInt16 => value is UInt16.MaxValue,
			SpecialType.System_Int16 => value is short s && unchecked((ushort)s) == UInt16.MaxValue,
			SpecialType.System_UInt32 => value is UInt32.MaxValue,
			SpecialType.System_Int32 => value is int i && unchecked((uint)i) == UInt32.MaxValue,
			SpecialType.System_UInt64 => value is UInt64.MaxValue,
			SpecialType.System_Int64 => value is long l && unchecked((ulong)l) == UInt64.MaxValue,
			_ => false
		};
	}
}