using System;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
///   x & ~0 = x and ~0 & x = x for integer types
/// </summary>
public class AndAllBitsSetStrategy : SymmetricStrategy<NumericBinaryStrategy, ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsAllBitsSet(context.Type.SpecialType, context.Right.Syntax.Token.Value))
		{
			optimized = null;
			return false;
		}

		optimized = context.Left.Syntax;
		return true;
	}

	private static bool IsAllBitsSet(SpecialType specialType, object? value)
	{
		return specialType switch
		{
			SpecialType.System_Byte => value is Byte.MaxValue,
			SpecialType.System_SByte => value is sbyte sb && unchecked((byte)sb) == Byte.MaxValue,
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