using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for nested modulo simplification: (x % m) % n where m % n == 0 => x % n
/// </summary>
public class ModuloNestedSimplificationStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		if (!context.Right.HasValue || context.Right.Value == null || context.Right.Value.IsNumericZero())
			return false;

		if (context.Left.Syntax is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } inner)
			return false;

		if (inner.Right is not LiteralExpressionSyntax innerRightLiteral)
			return false;

		var innerRightValue = innerRightLiteral.Token.Value;

		if (innerRightValue == null)
			return false;

		// Check if m % n == 0
		var mod = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.Remainder, innerRightValue, context.Right.Value);
		return mod != null && mod.IsNumericZero();
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var inner = (BinaryExpressionSyntax)context.Left.Syntax;
		return BinaryExpression(SyntaxKind.ModuloExpression, inner.Left, context.Right.Syntax);
	}
}
