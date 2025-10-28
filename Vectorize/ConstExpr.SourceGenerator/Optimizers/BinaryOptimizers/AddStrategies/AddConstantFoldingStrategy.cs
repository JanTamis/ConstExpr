using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for constant folding in chained additions: (x + C1) + C2 => x + (C1 + C2)
/// Also handles: C1 + (x + C2) => x + (C1 + C2) and C1 + (C2 + x) => x + (C1 + C2)
/// Additionally handles: (C1 + x) + C2 => x + (C1 + C2) when C1 is on the left
/// </summary>
public class AddConstantFoldingStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		// Pattern 1: (x + C1) + C2
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd
		    && context.TryGetLiteral(leftAdd.Right, out _))
		{
			return true;
		}

		// Pattern 1b: (C1 + x) + C2 - constant on left side of inner addition
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd2
		    && context.TryGetLiteral(leftAdd2.Left, out _))
		{
			return true;
		}

		// Pattern 2: C1 + (x + C2)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } rightAdd
		    && context.TryGetLiteral(rightAdd.Right, out _))
		{
			return true;
		}

		// Pattern 3: C1 + (C2 + x)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } rightAdd2
		    && context.TryGetLiteral(rightAdd2.Left, out _))
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// Pattern 1: (x + C1) + C2 => x + (C1 + C2)
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd
		    && context.TryGetLiteral(leftAdd.Right, out var leftConstant))
		{
			var c2 = context.Right.Value;

			if (leftConstant != null && c2 != null)
			{
				var result = leftConstant.Add(c2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.AddExpression,
						leftAdd.Left,
						newConstant);
				}
			}
		}

		// Pattern 1b: (C1 + x) + C2 => x + (C1 + C2) - constant on left side of inner addition
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd2
		    && context.TryGetLiteral(leftAdd2.Left, out var leftConstant2))
		{
			var c2 = context.Right.Value;

			if (leftConstant2 != null && c2 != null)
			{
				var result = leftConstant2.Add(c2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.AddExpression,
						leftAdd2.Right,
						newConstant);
				}
			}
		}

		// Pattern 2: C1 + (x + C2) => x + (C1 + C2)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } rightAdd
		    && context.TryGetLiteral(rightAdd.Right, out var rightConstant))
		{
			var c1 = context.Left.Value;

			if (c1 != null && rightConstant != null)
			{
				var result = c1.Add(rightConstant);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.AddExpression,
						rightAdd.Left,
						newConstant);
				}
			}
		}

		// Pattern 3: C1 + (C2 + x) => x + (C1 + C2)
		if (context.Left.HasValue 
		    && context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } rightAdd2
		    && context.TryGetLiteral(rightAdd2.Left, out var rightConstant2))
		{
			var c1 = context.Left.Value;

			if (c1 != null && rightConstant2 != null)
			{
				var result = c1.Add(rightConstant2);
				
				if (result != null)
				{
					var newConstant = SyntaxHelpers.CreateLiteral(result);
					return BinaryExpression(
						SyntaxKind.AddExpression,
						rightAdd2.Right,
						newConstant);
				}
			}
		}

		return null;
	}
}
