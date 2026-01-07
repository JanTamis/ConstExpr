using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
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
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
		{
			return false;
		}

		// Pattern 1: (x / C1) / C2 => x / (C1 * C2)
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } leftDiv
		    && context.TryGetValue(leftDiv.Right, out _))
		{
			return true;
		}

		// Pattern 2: (C1 / x) / C2 => (C1 / C2) / x
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } leftDiv2
		    && context.TryGetValue(leftDiv2.Left, out _))
		{
			return true;
		}

		// Pattern 3: C1 / (x / C2) => (C1 * C2) / x
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } rightDiv
		    && context.TryGetValue(rightDiv.Right, out _))
		{
			return true;
		}

		// Pattern 4: C1 / (C2 / x) => (C1 / C2) * x
		if (context.Left.HasValue 
		 && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } rightDiv2
		    && context.TryGetValue(rightDiv2.Left, out _))
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// Pattern 1: (x / C1) / C2 => x / (C1 * C2)
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } leftDiv
		    && context.TryGetValue(leftDiv.Right, out var leftConstant))
		{
			var c2 = context.Right.Value;

			if (leftConstant != null && c2 != null)
			{
				var result = leftConstant.Multiply(c2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.DivideExpression,
						leftDiv.Left,
						newConstant);
				}
			}
		}

		// Pattern 2: (C1 / x) / C2 => (C1 / C2) / x
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } leftDiv2
		    && context.TryGetValue(leftDiv2.Left, out var leftConstant2))
		{
			var c2 = context.Right.Value;

			if (leftConstant2 != null && c2 != null)
			{
				var result = leftConstant2.Divide(c2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.DivideExpression,
						newConstant,
						leftDiv2.Right);
				}
			}
		}

		// Pattern 3: C1 / (x / C2) => (C1 * C2) / x
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } rightDiv
		    && context.TryGetValue(rightDiv.Right, out var rightConstant))
		{
			var c1 = context.Left.Value;

			if (c1 != null && rightConstant != null)
			{
				var result = c1.Multiply(rightConstant);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.DivideExpression,
						newConstant,
						rightDiv.Left);
				}
			}
		}

		// Pattern 4: C1 / (C2 / x) => (C1 / C2) * x
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } rightDiv2
		    && context.TryGetValue(rightDiv2.Left, out var rightConstant2))
		{
			var c1 = context.Left.Value;

			if (c1 != null && rightConstant2 != null)
			{
				var result = c1.Divide(rightConstant2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.MultiplyExpression,
						newConstant,
						rightDiv2.Right);
				}
			}
		}

		return null;
	}
}
