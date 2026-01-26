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
public class AddConstantFoldingStrategy : NumericBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>
{
	// TODO Add symmerty
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
    {
      return false;
    }

    if (context.TryGetValue(context.Right.Syntax, out var c2)
		    && context.Left.Syntax.IsKind(SyntaxKind.AddExpression))
		{
			// Pattern 1: (x + C1) + C2 => x + (C1 + C2)
			if (context.TryGetValue(context.Left.Syntax.Right, out var leftConstant))
			{
				var result = leftConstant.Add(c2);
				var newConstant = SyntaxHelpers.CreateLiteral(result);

				optimized = BinaryExpression(SyntaxKind.AddExpression,
					context.Left.Syntax.Left,
					newConstant!);

				return true;
			}

			// Pattern 1b: (C1 + x) + C2 => x + (C1 + C2) - constant on left side of inner addition
			if (context.TryGetValue(context.Left.Syntax.Left, out var leftConstant2))
			{
				var result = leftConstant2.Add(c2);

				var newConstant = SyntaxHelpers.CreateLiteral(result);
				optimized = BinaryExpression(SyntaxKind.AddExpression,
					context.Left.Syntax.Right,
					newConstant!);

				return true;
			}
		}

		return false;
	}
}