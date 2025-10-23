using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryGreaterThanOrEqualOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.GreaterThanOrEqual;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsBoolType())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x >= x => true (pure)
		if (LeftEqualsRight(variables) && IsPure(Left))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		// Only apply arithmetic identities that are guaranteed safe for integer types.
		if (Type.IsInteger())
		{
			// x >= 0 = true (when x is unsigned)
			if (Type.IsUnsignedInteger() && rightValue.IsNumericZero())
			{
				result = SyntaxHelpers.CreateLiteral(true);
				return true;
			}

			// 0 >= x = false (when x is positive and non-zero and unsigned)
			if (leftValue.IsNumericZero() && hasRightValue && !rightValue.IsNumericZero() 
			 && (Type.IsUnsignedInteger() || ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, rightValue, 0.ToSpecialType(Type.SpecialType)) is true))
			{
				result = SyntaxHelpers.CreateLiteral(false);
				return true;
			}

			// x >= MinValue => true (always true for any value of the type)
			if (hasRightValue)
			{
				var isMinValue = Type.SpecialType switch
				{
					SpecialType.System_Byte => rightValue is byte.MinValue,
					SpecialType.System_SByte => rightValue is sbyte.MinValue,
					SpecialType.System_UInt16 => rightValue is ushort.MinValue,
					SpecialType.System_Int16 => rightValue is short.MinValue,
					SpecialType.System_UInt32 => rightValue is uint.MinValue,
					SpecialType.System_Int32 => rightValue is int.MinValue,
					SpecialType.System_UInt64 => rightValue is ulong.MinValue,
					SpecialType.System_Int64 => rightValue is long.MinValue,
					_ => false
				};

				if (isMinValue && IsPure(Left))
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}

			// MaxValue >= x => true (always true for any value of the type)
			if (hasLeftValue)
			{
				var isMaxValue = Type.SpecialType switch
				{
					SpecialType.System_Byte => leftValue is byte.MaxValue,
					SpecialType.System_SByte => leftValue is sbyte.MaxValue,
					SpecialType.System_UInt16 => leftValue is ushort.MaxValue,
					SpecialType.System_Int16 => leftValue is short.MaxValue,
					SpecialType.System_UInt32 => leftValue is uint.MaxValue,
					SpecialType.System_Int32 => leftValue is int.MaxValue,
					SpecialType.System_UInt64 => leftValue is ulong.MaxValue,
					SpecialType.System_Int64 => leftValue is long.MaxValue,
					_ => false
				};

				if (isMaxValue && IsPure(Right))
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}
		}

		return false;
	}
}

