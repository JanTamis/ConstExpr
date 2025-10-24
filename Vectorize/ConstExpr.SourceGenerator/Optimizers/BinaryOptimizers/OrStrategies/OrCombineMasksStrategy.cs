using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for combining constant masks: (x | mask1) | mask2 => x | (mask1 | mask2)
/// </summary>
public class OrCombineMasksStrategy : NumericOrBooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } leftOr
		       && context.Right.HasValue 
		       && context.Right.Value != null
		       && leftOr.Right is LiteralExpressionSyntax;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } leftOr)
			return null;

		if (leftOr.Right is not LiteralExpressionSyntax leftOrRightLiteral)
			return null;

		var leftOrRight = leftOrRightLiteral.Token.Value;

		if (leftOrRight == null)
			return null;

		var combined = ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.Or, leftOrRight, context.Right.Value);

		if (combined != null && SyntaxHelpers.TryGetLiteral(combined, out var combinedLiteral))
		{
			return BinaryExpression(SyntaxKind.BitwiseOrExpression, leftOr.Left, combinedLiteral);
		}

		return null;
	}
}
