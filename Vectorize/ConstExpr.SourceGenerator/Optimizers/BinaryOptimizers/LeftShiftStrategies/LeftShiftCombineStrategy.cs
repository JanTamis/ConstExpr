using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;

/// <summary>
/// Strategy for combining shifts: ((x << a) << b) => x << (a + b)
/// </summary>
public class LeftShiftCombineStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		if (context.Left.Syntax is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LeftShiftExpression } leftShift)
			return false;

		if (!context.Right.HasValue || context.Right.Value == null)
			return false;

		// Check if the inner shift has a constant shift amount
		// We'd need to evaluate leftShift.Right, but we don't have loader/variables here
		// For now, require that it's a literal
		return leftShift.Right is LiteralExpressionSyntax;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax leftShift)
			return null;

		if (leftShift.Right is not LiteralExpressionSyntax leftShiftLiteral)
			return null;

		var leftShiftValue = leftShiftLiteral.Token.Value;

		if (leftShiftValue == null)
			return null;

		var combined = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.Add, leftShiftValue, context.Right.Value);

		if (combined != null && SyntaxHelpers.TryGetLiteral(combined, out var combinedLiteral))
		{
			return BinaryExpression(SyntaxKind.LeftShiftExpression, leftShift.Left, combinedLiteral);
		}

		return null;
	}
}
