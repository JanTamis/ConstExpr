using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for idempotent modulo: (x % m) % m => x % m (when m is non-zero constant)
/// </summary>
public class ModuloIdempotencyStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		if (!context.Right.HasValue || context.Right.Value == null || context.Right.Value.IsNumericZero())
			return false;

		if (context.Left.Syntax is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } inner)
			return false;

		// Check if inner modulo has the same divisor
		if (inner.Right is LiteralExpressionSyntax innerRightLiteral)
		{
			var innerRightValue = innerRightLiteral.Token.Value;
			return EqualityComparer<object?>.Default.Equals(innerRightValue, context.Right.Value);
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// Return the inner modulo expression: (x % m) % m => x % m
		return context.Left.Syntax;
	}
}
