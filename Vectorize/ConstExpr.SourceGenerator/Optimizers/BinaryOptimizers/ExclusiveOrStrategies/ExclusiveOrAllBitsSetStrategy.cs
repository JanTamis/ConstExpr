using System;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
///   Strategy for all-bits-set: x ^ ~0 = ~x (integer types)
/// </summary>
public class ExclusiveOrAllBitsSetStrategy : SymmetricStrategy<IntegerBinaryStrategy, ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsAllBitsSet(context.Right.Syntax, context.Type.SpecialType))
		{
			optimized = null;
			return false;
		}

		optimized = BitwiseNotExpression(context.Left.Syntax);
		return true;
	}

	private static bool IsAllBitsSet(LiteralExpressionSyntax? value, SpecialType type)
	{
		return type switch
		{
			SpecialType.System_Byte => value?.Token.Value is Byte.MaxValue,
			SpecialType.System_SByte => value?.Token.Value is sbyte b && unchecked((byte)b) == Byte.MaxValue,
			SpecialType.System_UInt16 => value?.Token.Value is UInt16.MaxValue,
			SpecialType.System_Int16 => value?.Token.Value is short s && unchecked((ushort)s) == UInt16.MaxValue,
			SpecialType.System_UInt32 => value?.Token.Value is UInt32.MaxValue,
			SpecialType.System_Int32 => value?.Token.Value is int i && unchecked((uint)i) == UInt32.MaxValue,
			SpecialType.System_UInt64 => value?.Token.Value is UInt64.MaxValue,
			SpecialType.System_Int64 => value?.Token.Value is long l && unchecked((ulong)l) == UInt64.MaxValue,
			_ => false
		};
	}
}