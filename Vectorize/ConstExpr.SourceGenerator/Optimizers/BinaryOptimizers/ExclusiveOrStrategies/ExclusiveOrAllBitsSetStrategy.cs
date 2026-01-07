using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for all-bits-set: x ^ ~0 = ~x (integer types)
/// </summary>
public class ExclusiveOrAllBitsSetStrategy : SymmetricStrategy<IntegerBinaryStrategy, ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Right.Syntax.IsKind(SyntaxKind.BitwiseNotExpression)
		    && IsAllBitsSet(context.Right.Syntax, context.Type.SpecialType))
		{
			optimized = PrefixUnaryExpression(SyntaxKind.BitwiseNotExpression, context.Left.Syntax);
			return true;
		}

		optimized = null;
		return false;
	}

	private static bool IsAllBitsSet(LiteralExpressionSyntax? value, SpecialType type)
	{
		return type switch
		{
			SpecialType.System_Byte => value?.Token.Value is byte.MaxValue,
			SpecialType.System_SByte => value?.Token.Value is sbyte b && unchecked((byte)b) == byte.MaxValue,
			SpecialType.System_UInt16 => value?.Token.Value is ushort.MaxValue,
			SpecialType.System_Int16 => value?.Token.Value is short s && unchecked((ushort)s) == ushort.MaxValue,
			SpecialType.System_UInt32 => value?.Token.Value is uint.MaxValue,
			SpecialType.System_Int32 => value?.Token.Value is int i && unchecked((uint)i) == uint.MaxValue,
			SpecialType.System_UInt64 => value?.Token.Value is ulong.MaxValue,
			SpecialType.System_Int64 => value?.Token.Value is long l && unchecked((ulong)l) == ulong.MaxValue,
			_ => false
		};
	}
}
