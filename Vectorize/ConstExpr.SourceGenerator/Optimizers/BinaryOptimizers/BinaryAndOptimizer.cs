using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryAndOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.And;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// For integer/bool types
		if (Type.IsInteger() || Type.IsBoolType())
		{
			// x & 0 = 0
			if (hasRightValue && rightValue.IsNumericZero())
			{
				result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
				return true;
			}

			// 0 & x = 0
			if (hasLeftValue && leftValue.IsNumericZero())
			{
				result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
				return true;
			}

			// x & x = x (for pure expressions)
			if (Left.IsEquivalentTo(Right) && IsPure(Left))
			{
				result = Left;
				return true;
			}

			// For integer: x & ~0 (all bits set) = x
			if (Type.IsInteger() && hasRightValue)
			{
				var allBitsSet = Type.SpecialType switch
				{
					SpecialType.System_Byte => rightValue is byte.MaxValue,
					SpecialType.System_SByte => rightValue is sbyte b && unchecked((byte)b) == byte.MaxValue,
					SpecialType.System_UInt16 => rightValue is ushort.MaxValue,
					SpecialType.System_Int16 => rightValue is short s && unchecked((ushort)s) == ushort.MaxValue,
					SpecialType.System_UInt32 => rightValue is uint.MaxValue,
					SpecialType.System_Int32 => rightValue is int i && unchecked((uint)i) == uint.MaxValue,
					SpecialType.System_UInt64 => rightValue is ulong.MaxValue,
					SpecialType.System_Int64 => rightValue is long l && unchecked((ulong)l) == ulong.MaxValue,
					_ => false
				};

				if (allBitsSet)
				{
					result = Left;
					return true;
				}
			}

			// ~0 & x = x (all bits set on left)
			if (Type.IsInteger() && hasLeftValue)
			{
				var allBitsSet = Type.SpecialType switch
				{
					SpecialType.System_Byte => leftValue is byte.MaxValue,
					SpecialType.System_SByte => leftValue is sbyte b && unchecked((byte)b) == byte.MaxValue,
					SpecialType.System_UInt16 => leftValue is ushort.MaxValue,
					SpecialType.System_Int16 => leftValue is short s && unchecked((ushort)s) == ushort.MaxValue,
					SpecialType.System_UInt32 => leftValue is uint.MaxValue,
					SpecialType.System_Int32 => leftValue is int i && unchecked((uint)i) == uint.MaxValue,
					SpecialType.System_UInt64 => leftValue is ulong.MaxValue,
					SpecialType.System_Int64 => leftValue is long l && unchecked((ulong)l) == ulong.MaxValue,
					_ => false
				};

				if (allBitsSet)
				{
					result = Right;
					return true;
				}
			}

			// For bool: true & x = x, x & true = x
			if (Type.IsBoolType())
			{
				if (hasRightValue && rightValue is true)
				{
					result = Left;
					return true;
				}

				if (hasLeftValue && leftValue is true)
				{
					result = Right;
					return true;
				}

				// false & x = false (already covered by x & 0 = 0 above)
			}

			// x & (x | y) = x (absorption law, pure)
			if (Right is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } orRight
			    && IsPure(Left) && IsPure(orRight.Left) && IsPure(orRight.Right))
			{
				if (Left.IsEquivalentTo(orRight.Left) || Left.IsEquivalentTo(orRight.Right))
				{
					result = Left;
					return true;
				}
			}

			// (x | y) & x = x (absorption law, pure)
			if (Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } orLeft
			    && IsPure(Right) && IsPure(orLeft.Left) && IsPure(orLeft.Right))
			{
				if (Right.IsEquivalentTo(orLeft.Left) || Right.IsEquivalentTo(orLeft.Right))
				{
					result = Right;
					return true;
				}
			}
		}

		// Both sides are constant, evaluate
		if (hasLeftValue && hasRightValue)
		{
			var evalResult = ObjectExtensions.ExecuteBinaryOperation(Kind, leftValue, rightValue);
			if (evalResult != null)
			{
				result = SyntaxHelpers.CreateLiteral(evalResult);
				return true;
			}
		}

		return false;
	}
}
