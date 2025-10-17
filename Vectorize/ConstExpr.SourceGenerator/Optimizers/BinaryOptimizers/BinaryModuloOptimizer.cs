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

public class BinaryModuloOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Remainder;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsInteger())
		{
			return false;
		}

		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x % 1 = 0
		if (rightValue.IsNumericOne())
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// 0 % c = 0 (when c != 0)
		if (leftValue.IsNumericZero() && hasRightValue && !rightValue.IsNumericZero())
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// x % -1 = 0 for signed integer types
		if (rightValue.IsNumericNegativeOne())
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// Normalize negative constant divisor: x % (-m) => x % m (signed integers only)
		if (hasRightValue && !Type.IsUnsignedInteger())
		{
			var zero = 0.ToSpecialType(Type.SpecialType);

			if (ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, rightValue, zero) is true)
			{
				// Skip when rightValue is MinValue for the signed type (|MinValue| is not representable)
				var isMin = Type.SpecialType switch
				{
					SpecialType.System_SByte => rightValue is sbyte.MinValue,
					SpecialType.System_Int16 => rightValue is short.MinValue,
					SpecialType.System_Int32 => rightValue is int.MinValue,
					SpecialType.System_Int64 => rightValue is long.MinValue,
					_ => false
				};

				if (!isMin)
				{
					var abs = ObjectExtensions.Abs(rightValue, Type.SpecialType);

					if (abs is not null && SyntaxHelpers.TryGetLiteral(abs, out var absLiteral))
					{
						result = BinaryExpression(SyntaxKind.ModuloExpression, Left, absLiteral);
						return true;
					}
				}
			}
		}

		// (x % m) % m => x % m (idempotent), when m is a known non-zero constant
		if (hasRightValue && !rightValue.IsNumericZero() && Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.ModuloExpression } inner 
		    && inner.Right.TryGetLiteralValue(loader, variables, out var innerRightConst))
		{
			if (EqualityComparer<object?>.Default.Equals(innerRightConst, rightValue))
			{
				// Left is already the "x % m" expression
				result = Left;
				return true;
			}

			// (x % m) % n where m % n == 0 => x % n
			var mod = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.Remainder, innerRightConst, rightValue);

			if (mod is not null && mod.IsNumericZero())
			{
				result = BinaryExpression(SyntaxKind.ModuloExpression, inner.Left, Right);
				return true;
			}
		}

		// (x & (m-1)) % m => (x & (m-1)) for unsigned integers when m is power of two
		if (Type.IsUnsignedInteger() && rightValue.IsNumericPowerOfTwo(out _)
		    && Left is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseAndExpression } andOp)
		{
			var one = 1.ToSpecialType(Type.SpecialType);
			var mask = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.Subtract, rightValue, one);

			if (andOp.Left.TryGetLiteralValue(loader, variables, out var leftLit) && EqualityComparer<object?>.Default.Equals(leftLit, mask)
			    || andOp.Right.TryGetLiteralValue(loader, variables, out var rightLit) && EqualityComparer<object?>.Default.Equals(rightLit, mask))
			{
				result = Left; // already masked
				return true;
			}
		}

		// x % (power of two) => x & (power - 1) (for unsigned integers only)
		if (Type.IsUnsignedInteger() && rightValue.IsNumericPowerOfTwo(out var power))
		{
			// 2^power - 1 creates a mask with 'power' bits set
			var mask = (1 << power) - 1;
			var maskLiteral = SyntaxHelpers.CreateLiteral(mask.ToSpecialType(Type.SpecialType));

			if (maskLiteral != null)
			{
				result = BinaryExpression(SyntaxKind.BitwiseAndExpression, Left, maskLiteral);
				return true;
			}
		}

		return false;
	}
}