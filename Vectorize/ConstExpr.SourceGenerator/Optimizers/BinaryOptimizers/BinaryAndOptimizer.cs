using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryAndOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.And;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new ConditionalAndLiteralStrategy();
		yield return new ConditionalAndRightLiteralStrategy();
		yield return new ConditionalAndAbsorptionStrategy();
		yield return new ConditionalAndRedundancyStrategy();
		yield return new ConditionalAndContradictionStrategy();
		yield return new ConditionalAndIdempotencyStrategy();
	}

	//public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	//{
	//	result = null;

	//	var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
	//	var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

	//	// For integer/bool types
	//	if (Type.IsInteger() || Type.IsBoolType())
	//	{
	//		// x & 0 = 0
	//		if (rightValue.IsNumericZero())
	//		{
	//			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
	//			return true;
	//		}

	//		// 0 & x = 0
	//		if (leftValue.IsNumericZero())
	//		{
	//			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
	//			return true;
	//		}

	//		// x & x = x (for pure expressions)
	//		if (LeftEqualsRight(variables) && IsPure(Left))
	//		{
	//			result = Left;
	//			return true;
	//		}

	//		// For integer: x & ~0 (all bits set) = x
	//		if (Type.IsInteger() && hasRightValue)
	//		{
	//			var allBitsSet = Type.SpecialType switch
	//			{
	//				SpecialType.System_Byte => rightValue is byte.MaxValue,
	//				SpecialType.System_SByte => rightValue is sbyte b && unchecked((byte)b) == byte.MaxValue,
	//				SpecialType.System_UInt16 => rightValue is ushort.MaxValue,
	//				SpecialType.System_Int16 => rightValue is short s && unchecked((ushort)s) == ushort.MaxValue,
	//				SpecialType.System_UInt32 => rightValue is uint.MaxValue,
	//				SpecialType.System_Int32 => rightValue is int i && unchecked((uint)i) == uint.MaxValue,
	//				SpecialType.System_UInt64 => rightValue is ulong.MaxValue,
	//				SpecialType.System_Int64 => rightValue is long l && unchecked((ulong)l) == ulong.MaxValue,
	//				_ => false
	//			};

	//			if (allBitsSet)
	//			{
	//				result = Left;
	//				return true;
	//			}
	//		}

	//		// ~0 & x = x (all bits set on left)
	//		if (Type.IsInteger() && hasLeftValue)
	//		{
	//			var allBitsSet = Type.SpecialType switch
	//			{
	//				SpecialType.System_Byte => leftValue is byte.MaxValue,
	//				SpecialType.System_SByte => leftValue is sbyte b && unchecked((byte)b) == byte.MaxValue,
	//				SpecialType.System_UInt16 => leftValue is ushort.MaxValue,
	//				SpecialType.System_Int16 => leftValue is short s && unchecked((ushort)s) == ushort.MaxValue,
	//				SpecialType.System_UInt32 => leftValue is uint.MaxValue,
	//				SpecialType.System_Int32 => leftValue is int i && unchecked((uint)i) == uint.MaxValue,
	//				SpecialType.System_UInt64 => leftValue is ulong.MaxValue,
	//				SpecialType.System_Int64 => leftValue is long l && unchecked((ulong)l) == ulong.MaxValue,
	//				_ => false
	//			};

	//			if (allBitsSet)
	//			{
	//				result = Right;
	//				return true;
	//			}
	//		}

	//		// For bool: true & x = x, x & true = x
	//		if (Type.IsBoolType())
	//		{
	//			if (hasRightValue && rightValue is true)
	//			{
	//				result = Left;
	//				return true;
	//			}

	//			if (hasLeftValue && leftValue is true)
	//			{
	//				result = Right;
	//				return true;
	//			}

	//			// false & x = false (already covered by x & 0 = 0 above)
	//		}

	//		// x & (x | y) = x (absorption law, pure)
	//		if (Right is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } orRight
	//		    && IsPure(Left) && IsPure(orRight.Left) && IsPure(orRight.Right))
	//		{
	//			if (Left.IsEquivalentTo(orRight.Left) || Left.IsEquivalentTo(orRight.Right))
	//			{
	//				result = Left;
	//				return true;
	//			}
	//		}

	//		// (x | y) & x = x (absorption law, pure)
	//		if (Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } orLeft
	//		    && IsPure(Right) && IsPure(orLeft.Left) && IsPure(orLeft.Right))
	//		{
	//			if (Right.IsEquivalentTo(orLeft.Left) || Right.IsEquivalentTo(orLeft.Right))
	//			{
	//				result = Right;
	//				return true;
	//			}
	//		}
	//	}

	//	// (x & mask1) & mask2 => x & (mask1 & mask2) - combine masks
	//	if (Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseAndExpression } leftAnd
	//	    && hasRightValue && rightValue != null)
	//	{
	//		if (leftAnd.Right.TryGetLiteralValue(loader, variables, out var leftAndRight) && leftAndRight != null)
	//		{
	//			var combined = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.And, leftAndRight, rightValue);
	//			if (combined != null && SyntaxHelpers.TryGetLiteral(combined, out var combinedLiteral))
	//			{
	//				result = SyntaxFactory.BinaryExpression(SyntaxKind.BitwiseAndExpression, leftAnd.Left, combinedLiteral);
	//				return true;
	//			}
	//		}
	//	}

	//	// (x | mask) & mask => mask (when x is pure)
	//	if (hasRightValue && rightValue != null
	//	    && Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseOrExpression } leftOr
	//	    && IsPure(leftOr.Left))
	//	{
	//		if (leftOr.Right.TryGetLiteralValue(loader, variables, out var orMask)
	//		    && EqualityComparer<object?>.Default.Equals(orMask, rightValue))
	//		{
	//			result = Right;
	//			return true;
	//		}
	//	}

	//	// (x | mask1) & mask2 when mask1 & mask2 == 0 => x & mask2 (when x is pure)
	//	if (hasRightValue && rightValue != null
	//	    && Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseOrExpression } leftOr2
	//	    && IsPure(leftOr2.Left))
	//	{
	//		if (leftOr2.Right.TryGetLiteralValue(loader, variables, out var orMask2) && orMask2 != null)
	//		{
	//			var intersection = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.And, orMask2, rightValue);
	//			if (intersection is not null && intersection.IsNumericZero())
	//			{
	//				result = SyntaxFactory.BinaryExpression(SyntaxKind.BitwiseAndExpression, leftOr2.Left, Right);
	//				return true;
	//			}
	//		}
	//	}

	//	return false;
	//}
}
