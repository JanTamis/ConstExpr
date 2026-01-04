using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for all-bits-set absorption: x | ~0 = ~0 (integer types)
/// </summary>
public class OrAllBitsSetStrategy : IntegerBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
			return false;
		
		if (context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		    && IsAllBitsSet(rightValue, context.Type.SpecialType))
		{
			optimized = context.Right.Syntax;
			return true;
		}
		
		if (context.TryGetLiteral(context.Left.Syntax, out var leftValue)
		    && IsAllBitsSet(leftValue, context.Type.SpecialType))
		{
			optimized = context.Left.Syntax;
			return true;
		}
		
		return false;
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
