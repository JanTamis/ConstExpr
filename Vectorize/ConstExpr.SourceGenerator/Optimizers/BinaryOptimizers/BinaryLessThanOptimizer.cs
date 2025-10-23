using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryLessThanOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.LessThan;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x < x => false (for pure expressions)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// Only apply arithmetic identities that are guaranteed safe for integer types.
		if (Type.IsInteger())
		{
			// x < 0 = false (when x is unsigned)
			if (Type.IsUnsignedInteger() && rightValue.IsNumericZero())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}

			// 0 < x = true (when x is positive and non-zero and unsigned)
			if (leftValue.IsNumericZero() && hasRightValue && !rightValue.IsNumericZero())
			{
				if (Type.IsUnsignedInteger() || ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, rightValue, 0.ToSpecialType(Type.SpecialType)) is true)
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}

			// x < -1 = false for signed integer types
			if (!Type.IsUnsignedInteger() && rightValue.IsNumericNegativeOne())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}

			// MinValue < x = true (for x > MinValue in signed types)
			if (hasLeftValue && hasRightValue && !rightValue.IsNumericZero())
			{
				// If left is the minimum value of the type and right is not, then left < right is always true
				var isLeftMin = Type.SpecialType switch
				{
					SpecialType.System_SByte => leftValue is sbyte.MinValue,
					SpecialType.System_Int16 => leftValue is short.MinValue,
					SpecialType.System_Int32 => leftValue is int.MinValue,
					SpecialType.System_Int64 => leftValue is long.MinValue,
					_ => false
				};

				if (isLeftMin && !Type.IsUnsignedInteger())
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}

			// x < MaxValue = true for x != MaxValue
			if (hasRightValue && hasLeftValue)
			{
				var isRightMax = Type.SpecialType switch
				{
					SpecialType.System_SByte => rightValue is sbyte.MaxValue,
					SpecialType.System_Byte => rightValue is byte.MaxValue,
					SpecialType.System_Int16 => rightValue is short.MaxValue,
					SpecialType.System_UInt16 => rightValue is ushort.MaxValue,
					SpecialType.System_Int32 => rightValue is int.MaxValue,
					SpecialType.System_UInt32 => rightValue is uint.MaxValue,
					SpecialType.System_Int64 => rightValue is long.MaxValue,
					SpecialType.System_UInt64 => rightValue is ulong.MaxValue,
					_ => false
				};

				if (isRightMax && !EqualityComparer<object?>.Default.Equals(leftValue, rightValue))
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}
		}

		return false;
	}
}