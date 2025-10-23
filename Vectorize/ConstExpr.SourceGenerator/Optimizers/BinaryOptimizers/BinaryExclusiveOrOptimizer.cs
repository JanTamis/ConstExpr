using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryExclusiveOrOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.ExclusiveOr;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// For integer/bool types
		if (Type.IsInteger() || Type.IsBoolType())
		{
			// x ^ 0 = x
			if (rightValue.IsNumericZero())
			{
				result = Left;
				return true;
			}

			// 0 ^ x = x
			if (leftValue.IsNumericZero())
			{
				result = Right;
				return true;
			}

			// x ^ x = 0 (for pure expressions)
			if (LeftEqualsRight(variables) && IsPure(Left))
			{
				result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
				return true;
			}

			// For integer: x ^ ~0 (all bits set) = ~x
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
					result = PrefixUnaryExpression(SyntaxKind.BitwiseNotExpression, Left);
					return true;
				}
			}

			// ~0 ^ x = ~x (all bits set on left)
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
					result = PrefixUnaryExpression(SyntaxKind.BitwiseNotExpression, Right);
					return true;
				}
			}

			// For bool: false ^ x = x, x ^ false = x (same as 0 case above)
			// For bool: true ^ x = !x, x ^ true = !x
			if (Type.IsBoolType())
			{
				if (hasRightValue && rightValue is true)
				{
					result = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Left);
					return true;
				}

				if (hasLeftValue && leftValue is true)
				{
					result = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Right);
					return true;
				}
			}

			// x ^ (x ^ y) = y (associative cancellation, pure)
			if (Right is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ExclusiveOrExpression } xorRight
			    && IsPure(Left) && IsPure(xorRight.Left) && IsPure(xorRight.Right))
			{
				if (Left.IsEquivalentTo(xorRight.Left))
				{
					result = xorRight.Right;
					return true;
				}
				if (Left.IsEquivalentTo(xorRight.Right))
				{
					result = xorRight.Left;
					return true;
				}
			}

			// (x ^ y) ^ x = y (associative cancellation, pure)
			if (Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ExclusiveOrExpression } xorLeft
			    && IsPure(Right) && IsPure(xorLeft.Left) && IsPure(xorLeft.Right))
			{
				if (Right.IsEquivalentTo(xorLeft.Left))
				{
					result = xorLeft.Right;
					return true;
				}
				if (Right.IsEquivalentTo(xorLeft.Right))
				{
					result = xorLeft.Left;
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

		// (x ^ mask1) ^ mask2 => x ^ (mask1 ^ mask2) - combine constant masks
		if (Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.ExclusiveOrExpression } leftXor
		    && hasRightValue && rightValue != null)
		{
			if (leftXor.Right.TryGetLiteralValue(loader, variables, out var leftXorRight) && leftXorRight != null)
			{
				var combined = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.ExclusiveOr, leftXorRight, rightValue);
				if (combined != null && SyntaxHelpers.TryGetLiteral(combined, out var combinedLiteral))
				{
					result = SyntaxFactory.BinaryExpression(SyntaxKind.ExclusiveOrExpression, leftXor.Left, combinedLiteral);
					return true;
				}
			}
		}

		return false;
	}
}
