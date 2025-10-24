using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for double negation: x - -y => x + y (pure)
/// </summary>
public class SubtractDoubleNegationStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } pre
		       && IsPure(context.Left.Syntax) 
		       && IsPure(pre.Operand);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var pre = (PrefixUnaryExpressionSyntax)context.Right.Syntax;
		return BinaryExpression(SyntaxKind.AddExpression, context.Left.Syntax, pre.Operand);
	}
}
