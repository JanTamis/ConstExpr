using System;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
///   Strategy for bitwise self-complement: x | ~x = all-bits-set (pure). Every bit that's
///   clear in x is set in ~x and vice versa, so the OR always sets every bit.
/// </summary>
public class OrSelfComplementStrategy() : SymmetricStrategy<IntegerBinaryStrategy, ExpressionSyntax, PrefixUnaryExpressionSyntax>(rightKind: SyntaxKind.BitwiseNotExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context.Left.Syntax, context.Right.Syntax.Operand, context.Variables)
		    || !IsPure(context.Left.Syntax)
		    || !TryCreateAllBitsSetLiteral(context.Type.SpecialType, out var literal))
		{
			optimized = null;
			return false;
		}

		optimized = literal;
		return true;
	}

	private static bool TryCreateAllBitsSetLiteral(SpecialType specialType, out ExpressionSyntax? literal)
	{
		object? value = specialType switch
		{
			SpecialType.System_Byte => Byte.MaxValue,
			SpecialType.System_SByte => (sbyte) -1,
			SpecialType.System_UInt16 => UInt16.MaxValue,
			SpecialType.System_Int16 => (short) -1,
			SpecialType.System_UInt32 => UInt32.MaxValue,
			SpecialType.System_Int32 => -1,
			SpecialType.System_UInt64 => UInt64.MaxValue,
			SpecialType.System_Int64 => -1L,
			_ => null
		};

		return TryCreateLiteral(value, out literal);
	}
}
