using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for already masked values: (x & (m-1)) % m => (x & (m-1)) for unsigned integers when m is power of two
/// </summary>
public class ModuloAlreadyMaskedStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		if (!context.Type.IsUnsignedInteger())
			return false;

		if (!context.Right.HasValue || !context.Right.Value.IsNumericPowerOfTwo(out _))
			return false;

		if (context.Left.Syntax is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andOp)
			return false;

		// Calculate m - 1
		var one = 1.ToSpecialType(context.Type.SpecialType);
		var mask = ObjectExtensions.ExecuteBinaryOperation(Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.Subtract, context.Right.Value, one);

		if (mask == null)
			return false;

		// Check if either operand of the AND matches the mask
		if (andOp.Left is LiteralExpressionSyntax leftLit)
		{
			if (EqualityComparer<object?>.Default.Equals(leftLit.Token.Value, mask))
				return true;
		}

		if (andOp.Right is LiteralExpressionSyntax rightLit)
		{
			if (EqualityComparer<object?>.Default.Equals(rightLit.Token.Value, mask))
				return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// The value is already masked, so just return it
		return context.Left.Syntax;
	}
}
