using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for already masked values: (x & (m-1)) % m => (x & (m-1)) for unsigned integers when m is power of two
/// </summary>
public class ModuloAlreadyMaskedStrategy() : UnsigedIntegerBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.BitwiseAndExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		// Base type validation
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetValue(context.Right.Syntax, out var rightValue) 
		    || !rightValue.IsNumericPowerOfTwo(out _))
			return false;

		// Calculate m - 1
		var one = 1.ToSpecialType(context.Type.SpecialType);
		var mask = rightValue.Subtract(one);

		if (mask == null)
			return false;

		// Check if either operand of the AND matches the mask
		if (context.TryGetValue(context.Left.Syntax.Left, out var leftAndValue) 
		    && EqualityComparer<object?>.Default.Equals(leftAndValue, mask)
		    || context.TryGetValue(context.Left.Syntax.Right, out var rightAndValue)
		    && EqualityComparer<object?>.Default.Equals(rightAndValue, mask))
		{
			optimized = context.Left.Syntax;
			return true;
		}

		return false;
	}
}
