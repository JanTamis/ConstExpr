using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for absorption law: x | (x & y) = x and (x & y) | x = x (pure)
/// </summary>
public class OrAbsorptionStrategy : NumericOrBooleanBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
			return false;

		// x | (x & y) = x
		if (context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseAndExpression } andRight
		    && (LeftEqualsRight(context.Left.Syntax, andRight.Left, context.TryGetLiteral) 
		        || LeftEqualsRight(context.Left.Syntax, andRight.Right, context.TryGetLiteral)))
		{
			optimized = context.Left.Syntax;
			return true;
		}

		// (x & y) | x = x
		if (context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseAndExpression } andLeft
		    && (LeftEqualsRight(context.Right.Syntax, andLeft.Left, context.TryGetLiteral) 
		        || LeftEqualsRight(context.Right.Syntax, andLeft.Right, context.TryGetLiteral)))
		{
			optimized = context.Right.Syntax;
			return true;
		}
		
		return false;
	}
}
