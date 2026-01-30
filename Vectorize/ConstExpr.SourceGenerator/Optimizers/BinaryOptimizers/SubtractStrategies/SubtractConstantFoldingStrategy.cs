using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for constant folding in chained subtractions: (x - C1) - C2 => x - (C1 + C2)
/// Also handles: (C1 - x) - C2 => (C1 - C2) - x and C1 - (x - C2) => (C1 + C2) - x
/// Note: subtraction is not commutative, so patterns must preserve order carefully
/// </summary>
public class SubtractConstantFoldingStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.SubtractExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.TryGetValue(context.Left.Syntax.Left, out var leftConstant)
		    && SyntaxHelpers.TryGetLiteral(leftConstant.Subtract(context.Right.Syntax.Token.Value), out var combinedLiteral))
		{
			optimized = BinaryExpression(SyntaxKind.SubtractExpression, context.Left.Syntax.Right, combinedLiteral);
			return true;
		}

		if (context.TryGetValue(context.Left.Syntax.Right, out var leftConstant2)
		    && SyntaxHelpers.TryGetLiteral(leftConstant2.Subtract(context.Right.Syntax.Token.Value), out var combinedLiteral2))
		{
			optimized = BinaryExpression(SyntaxKind.SubtractExpression, context.Left.Syntax.Left, combinedLiteral2);
			return true;
		}

		optimized = null;
		return false;
	}
}
