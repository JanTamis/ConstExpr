using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryLessThanOrEqualOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.LessThanOrEqual;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x <= x => true (for pure expressions)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		// Only apply arithmetic identities that are guaranteed safe for integer types.
		if (Type.IsInteger())
		{
			// x <= -1 = false (when x is unsigned)
			if (Type.IsUnsignedInteger() && rightValue.IsNumericNegativeOne())
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}

			// 0 <= x = true (when x is unsigned)
			if (Type.IsUnsignedInteger() && leftValue.IsNumericZero())
			{
				result = SyntaxHelpers.CreateLiteral(true);
				return true;
			}

			// x <= MaxValue = true (always true for any x <= MaxValue)
			if (hasRightValue)
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
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}

			// MinValue <= x = true (MinValue is always <= any value)
			if (hasLeftValue)
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
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}
		}

		return false;
	}
}