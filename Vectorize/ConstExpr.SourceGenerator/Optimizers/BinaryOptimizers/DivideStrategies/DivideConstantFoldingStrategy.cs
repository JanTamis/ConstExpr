using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for constant folding in chained divisions: (x / C1) / C2 => x / (C1 * C2)
/// Also handles: (C1 / x) / C2 => (C1 / C2) / x and C1 / (x / C2) => (C1 * C2) / x
/// Note: division is not commutative, so patterns must preserve order carefully
/// </summary>
public class DivideConstantFoldingStrategy : NumericBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
    {
      return false;
    }

    // Pattern 1: (x / C1) / C2 => x / (C1 * C2)
    if (context.TryGetValue(context.Right.Syntax, out var c2)
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.DivideExpression } leftDiv
		    && context.TryGetValue(leftDiv.Right, out var leftConstant))
		{
			var result = leftConstant.Multiply(c2);

			if (result != null && SyntaxHelpers.TryGetLiteral(result, out var newConstant))
			{
				optimized = BinaryExpression(
					SyntaxKind.DivideExpression,
					leftDiv.Left,
					newConstant);

				return true;
			}
		}

		// Pattern 2: (C1 / x) / C2 => (C1 / C2) / x
		if (context.TryGetValue(context.Right.Syntax, out c2)
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.DivideExpression } leftDiv2
		    && context.TryGetValue(leftDiv2.Left, out var leftConstant2))
		{
			var result = leftConstant2.Divide(c2);

			if (result != null && SyntaxHelpers.TryGetLiteral(result, out var newConstant))
			{
				optimized = BinaryExpression(
					SyntaxKind.DivideExpression,
					newConstant,
					leftDiv2.Right);

				return true;
			}
		}

		// Pattern 3: C1 / (x / C2) => (C1 * C2) / x
		if (context.TryGetValue(context.Left.Syntax, out var c1)
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.DivideExpression } rightDiv
		    && context.TryGetValue(rightDiv.Right, out var rightConstant))
		{
			var result = c1.Multiply(rightConstant);

			if (result != null && SyntaxHelpers.TryGetLiteral(result, out var newConstant))
			{
				optimized = BinaryExpression(
					SyntaxKind.DivideExpression,
					newConstant,
					rightDiv.Left);

				return true;
			}
		}

		// Pattern 4: C1 / (C2 / x) => (C1 / C2) * x
		if (context.TryGetValue(context.Left.Syntax, out c1)
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.DivideExpression } rightDiv2
		    && context.TryGetValue(rightDiv2.Left, out var rightConstant2))
		{
			var result = c1.Divide(rightConstant2);

			if (result != null && SyntaxHelpers.TryGetLiteral(result, out var newConstant))
			{
				optimized = BinaryExpression(
					SyntaxKind.MultiplyExpression,
					newConstant,
					rightDiv2.Right);
				
				return true;
			}
		}

		// Pattern 5: (x * C1) / C2 => x * (C1 / C2)
		if (context.TryGetValue(context.Right.Syntax, out c2)
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } leftMul
		    && context.TryGetValue(leftMul.Right, out var mulConstant))
		{
			var result = mulConstant.Divide(c2);

			if (result != null && SyntaxHelpers.TryGetLiteral(result, out var newConstant))
			{
				optimized = BinaryExpression(
					SyntaxKind.MultiplyExpression,
					leftMul.Left,
					newConstant);

				return true;
			}
		}

		// Pattern 6: (C1 * x) / C2 => x * (C1 / C2)
		if (context.TryGetValue(context.Right.Syntax, out c2)
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } leftMul2
		    && context.TryGetValue(leftMul2.Left, out var mulConstant2))
		{
			var result = mulConstant2.Divide(c2);

			if (result != null && SyntaxHelpers.TryGetLiteral(result, out var newConstant))
			{
				optimized = BinaryExpression(
					SyntaxKind.MultiplyExpression,
					leftMul2.Right,
					newConstant);

				return true;
			}
		}
		
		return false;
	}
}