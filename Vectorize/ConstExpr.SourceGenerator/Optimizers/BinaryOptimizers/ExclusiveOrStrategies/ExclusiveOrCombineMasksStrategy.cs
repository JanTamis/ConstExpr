using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for combining constant masks: (x ^ mask1) ^ mask2 => x ^ (mask1 ^ mask2)
/// </summary>
public class ExclusiveOrCombineMasksStrategy : NumericOrBooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ExclusiveOrExpression } leftXor
		       && context.Right.HasValue 
		       && context.Right.Value != null
		       && leftXor.Right is LiteralExpressionSyntax;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ExclusiveOrExpression } leftXor)
			return null;

		if (leftXor.Right is not LiteralExpressionSyntax leftXorRightLiteral)
			return null;

		var leftXorRight = leftXorRightLiteral.Token.Value;

		if (leftXorRight == null)
			return null;

		var combined = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.ExclusiveOr, leftXorRight, context.Right.Value);

		if (combined != null && SyntaxHelpers.TryGetLiteral(combined, out var combinedLiteral))
		{
			return BinaryExpression(SyntaxKind.ExclusiveOrExpression, leftXor.Left, combinedLiteral);
		}

		return null;
	}
}
