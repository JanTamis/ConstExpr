using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for constant folding in chained multiplications: (x * C1) * C2 => x * (C1 * C2)
/// Also handles: C1 * (x * C2) => x * (C1 * C2) and C1 * (C2 * x) => x * (C1 * C2)
/// </summary>
public class MultiplyConstantFoldingStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		// Pattern 1: (x * C1) * C2
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } leftMult
		    && leftMult.Right is LiteralExpressionSyntax
		    && IsPure(leftMult.Left))
		{
			return true;
		}

		// Pattern 2: C1 * (x * C2)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMult
		    && rightMult.Right is LiteralExpressionSyntax
		    && IsPure(rightMult.Left))
		{
			return true;
		}

		// Pattern 3: C1 * (C2 * x)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMult2
		    && rightMult2.Left is LiteralExpressionSyntax
		    && IsPure(rightMult2.Right))
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// Pattern 1: (x * C1) * C2 => x * (C1 * C2)
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } leftMult
		    && leftMult.Right is LiteralExpressionSyntax leftConstant)
		{
			var c1 = leftConstant.Token.Value;
			var c2 = context.Right.Value;

			if (c1 != null && c2 != null)
			{
				var result = c1.Multiply(c2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.MultiplyExpression,
						leftMult.Left,
						newConstant);
				}
			}
		}

		// Pattern 2: C1 * (x * C2) => x * (C1 * C2)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMult
		    && rightMult.Right is LiteralExpressionSyntax rightConstant)
		{
			var c1 = context.Left.Value;
			var c2 = rightConstant.Token.Value;

			if (c1 != null && c2 != null)
			{
				var result = c1.Multiply(c2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.MultiplyExpression,
						rightMult.Left,
						newConstant);
				}
			}
		}

		// Pattern 3: C1 * (C2 * x) => x * (C1 * C2)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMult2
		    && rightMult2.Left is LiteralExpressionSyntax rightConstant2)
		{
			var c1 = context.Left.Value;
			var c2 = rightConstant2.Token.Value;

			if (c1 != null && c2 != null)
			{
				var result = c1.Multiply(c2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.MultiplyExpression,
						rightMult2.Right,
						newConstant);
				}
			}
		}

		return null;
	}
}
