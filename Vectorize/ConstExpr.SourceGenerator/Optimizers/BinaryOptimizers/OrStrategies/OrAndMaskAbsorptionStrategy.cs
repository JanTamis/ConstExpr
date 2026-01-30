using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for mask absorption: (x & mask) | mask => mask (when x is pure)
/// </summary>
public class OrAndMaskAbsorptionStrategy() : SymmetricStrategy<NumericOrBooleanBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.BitwiseAndExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax.Left)
		    || ! context.TryGetValue(context.Left.Syntax.Right, out var leftAndRightLiteral)
		    || !EqualityComparer<object?>.Default.Equals(leftAndRightLiteral, context.Right.Syntax.Token.Value))
		{
			optimized = null;
			return false;
		}
		
		optimized = context.Right.Syntax;
		return true;
	}
}
