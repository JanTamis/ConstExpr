using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for algebraic identity: (a + x) - a => x (pure)
/// </summary>
public class SubtractAdditionCancellationLeftStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } leftAdd
		       && leftAdd.Left.IsEquivalentTo(context.Right.Syntax)
		       && IsPure(leftAdd.Left) 
		       && IsPure(leftAdd.Right) 
		       && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var leftAdd = (BinaryExpressionSyntax)context.Left.Syntax;
		return leftAdd.Right;
	}
}
