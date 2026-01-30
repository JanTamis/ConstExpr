using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for constant folding when subtracting from an addition: (x + C1) - C2 => x + (C1 - C2)
/// Also handles: (C1 + x) - C2 => x + (C1 - C2)
/// </summary>
public class SubtractFromAdditionConstantFoldingStrategy() : NumericBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.AddExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
    {
      return false;
    }

    if (context.TryGetValue(context.Left.Syntax.Left, out var leftConstant))
		{
			var result = leftConstant.Subtract(context.Right.Syntax.Token.Value);

			if (SyntaxHelpers.TryGetLiteral(result, out var newConstant))
			{
				optimized = BinaryExpression(SyntaxKind.AddExpression, context.Left.Syntax.Right, newConstant);
				return true;
			}
		}
		
		if (context.TryGetValue(context.Left.Syntax.Right, out var leftConstant2))
		{
			var result = leftConstant2.Subtract(context.Right.Syntax.Token.Value);

			if (SyntaxHelpers.TryGetLiteral(result, out var newConstant))
			{
				optimized = BinaryExpression(SyntaxKind.AddExpression, context.Left.Syntax.Left, newConstant);
				return true;
			}
		}
		
		return false;
	}
}

