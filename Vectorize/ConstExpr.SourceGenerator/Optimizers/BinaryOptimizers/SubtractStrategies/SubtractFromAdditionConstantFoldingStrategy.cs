using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for constant folding when subtracting from an addition: (x + C1) - C2 => x + (C1 - C2)
/// Also handles: (C1 + x) - C2 => x + (C1 - C2)
/// </summary>
public class SubtractFromAdditionConstantFoldingStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		// Pattern 1: (x + C1) - C2 => x + (C1 - C2)
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd
		    && context.TryGetLiteral(leftAdd.Right, out _))
		{
			return true;
		}

		// Pattern 2: (C1 + x) - C2 => x + (C1 - C2)
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd2
		    && context.TryGetLiteral(leftAdd2.Left, out _))
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// Pattern 1: (x + C1) - C2 => x + (C1 - C2)
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd
		    && context.TryGetLiteral(leftAdd.Right, out var c1))
		{
			var c2 = context.Right.Value;

			if (c1 != null && c2 != null)
			{
				var result = c1.Subtract(c2);
				
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

		// Pattern 2: (C1 + x) - C2 => x + (C1 - C2)
		if (context.Right.HasValue 
		    && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd2
		    && context.TryGetLiteral(leftAdd2.Left, out var c1_2))
		{
			var c2 = context.Right.Value;

			if (c1_2 != null && c2 != null)
			{
				var result = c1_2.Subtract(c2);
				
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

		return null;
	}
}

