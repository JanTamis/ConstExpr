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
/// Additionally handles: (C1 * x) * C2 => x * (C1 * C2) and (C1 * x) * C2 when C1 is on the left
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
		 && context.TryGetValue(leftMult.Right, out _))
		{
			return true;
		}

		// Pattern 1b: (C1 * x) * C2 - constant on left side of inner multiply
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } leftMult2
		    && context.TryGetValue(leftMult2.Left, out _))
		{
			return true;
		}

		// Pattern 2: C1 * (x * C2)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMult
		    && context.TryGetValue(rightMult.Right, out _))
		{
			return true;
		}

		// Pattern 3: C1 * (C2 * x)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMult2
		    && context.TryGetValue(rightMult2.Left, out _))
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
		    && context.TryGetValue(leftMult.Right, out var leftConstant))
		{
			var c2 = context.Right.Value;

			if (leftConstant != null && c2 != null)
			{
				var result = leftConstant.Multiply(c2);
				
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

		// Pattern 1b: (C1 * x) * C2 => x * (C1 * C2) - constant on left side of inner multiply
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } leftMult2
		    && context.TryGetValue(leftMult2.Left, out var leftConstant2))
		{
			var c2 = context.Right.Value;

			if (leftConstant2 != null && c2 != null)
			{
				var result = leftConstant2.Multiply(c2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.MultiplyExpression,
						leftMult2.Right,
						newConstant);
				}
			}
		}

		// Pattern 2: C1 * (x * C2) => x * (C1 * C2)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMult
		    && context.TryGetValue(rightMult.Right, out var rightConstant))
		{
			var c1 = context.Left.Value;

			if (c1 != null && rightConstant != null)
			{
				var result = c1.Multiply(rightConstant);
				
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
		    && context.TryGetValue(rightMult2.Left, out var rightConstant2))
		{
			var c1 = context.Left.Value;

			if (c1 != null && rightConstant2 != null)
			{
				var result = c1.Multiply(rightConstant2);
				
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
