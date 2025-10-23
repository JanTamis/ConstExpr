using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryGreaterThanOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.GreaterThan;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x > x => false (for pure expressions)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(false);
			return true;
		}

		// Only apply arithmetic identities that are guaranteed safe for integer types.
		if (Type.IsInteger())
		{
			// x > -1 = false (when x is unsigned) [symmetry with x < 0 = false]
			if (Type.IsUnsignedInteger() && rightValue.IsNumericNegativeOne())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}

			// x > 0 = true (when x is positive and non-zero and unsigned)
			if (rightValue.IsNumericZero() && hasLeftValue && !leftValue.IsNumericZero())
			{
				if (Type.IsUnsignedInteger() || ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, leftValue, 0.ToSpecialType(Type.SpecialType)) is true)
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}

			// -1 > x = false for signed integer types
			if (!Type.IsUnsignedInteger() && leftValue.IsNumericNegativeOne())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}

			// x > MaxValue = false (for x <= MaxValue)
			if (hasLeftValue && hasRightValue)
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

				if (isRightMax)
				{
					result = SyntaxHelpers.CreateLiteral(false);
					return true;
				}
			}

			// MinValue > x = false (MinValue cannot be greater than anything)
			if (hasLeftValue && hasRightValue && !rightValue.IsNumericZero())
			{
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
					result = SyntaxHelpers.CreateLiteral(false);
					return true;
				}
			}
		}

		return false;
	}
}
