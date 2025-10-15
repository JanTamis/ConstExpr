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

public class BinaryOrOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Or;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// For integer/bool types
		if (Type.IsInteger() || Type.IsBoolType())
		{
			// x | 0 = x
			if (rightValue.IsNumericZero())
			{
				result = Left;
				return true;
			}

			// 0 | x = x
			if (leftValue.IsNumericZero())
			{
				result = Right;
				return true;
			}

			// x | x = x (for pure expressions)
			if (LeftEqualsRight(variables) && IsPure(Left))
			{
				result = Left;
				return true;
			}

			// For integer: x | ~0 (all bits set) = ~0
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
					result = Right;
					return true;
				}
			}

			// ~0 | x = ~0 (all bits set on left)
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
					result = Left;
					return true;
				}
			}

			// For bool: false | x = x, x | false = x
			if (Type.IsBoolType())
			{
				if (rightValue is false)
				{
					result = Left;
					return true;
				}

				if (leftValue is false)
				{
					result = Right;
					return true;
				}

				// true | x = true, x | true = true
				if (rightValue is true)
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}

				if (leftValue is true)
				{
					result = SyntaxHelpers.CreateLiteral(true);
					return true;
				}
			}

			// x | (x & y) = x (absorption law, pure)
			if (Right is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andRight
			    && IsPure(Left) && IsPure(andRight.Left) && IsPure(andRight.Right))
			{
				if (Left.IsEquivalentTo(andRight.Left) || Left.IsEquivalentTo(andRight.Right))
				{
					result = Left;
					return true;
				}
			}

			// (x & y) | x = x (absorption law, pure)
			if (Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andLeft
			    && IsPure(Right) && IsPure(andLeft.Left) && IsPure(andLeft.Right))
			{
				if (Right.IsEquivalentTo(andLeft.Left) || Right.IsEquivalentTo(andLeft.Right))
				{
					result = Right;
					return true;
				}
			}
		}

		return false;
	}
}
