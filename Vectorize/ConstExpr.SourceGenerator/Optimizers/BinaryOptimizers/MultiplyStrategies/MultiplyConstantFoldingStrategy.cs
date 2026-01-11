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
public class MultiplyConstantFoldingStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.MultiplyExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.TryGetValue(context.Left.Syntax.Left, out var leftConstant)
		    && SyntaxHelpers.TryGetLiteral(leftConstant.Multiply(context.Right.Syntax.Token.Value), out var combinedLiteral))
		{
			optimized = BinaryExpression(SyntaxKind.MultiplyExpression, context.Left.Syntax.Right, combinedLiteral);
			return true;
		}
		
		if (context.TryGetValue(context.Left.Syntax.Right, out var leftConstant2)
		    && SyntaxHelpers.TryGetLiteral(leftConstant2.Multiply(context.Right.Syntax.Token.Value), out var combinedLiteral2))
		{
			optimized = BinaryExpression(SyntaxKind.MultiplyExpression, context.Left.Syntax.Left, combinedLiteral2);
			return true;
		}
		
		optimized = null;
		return false;
	}
}
