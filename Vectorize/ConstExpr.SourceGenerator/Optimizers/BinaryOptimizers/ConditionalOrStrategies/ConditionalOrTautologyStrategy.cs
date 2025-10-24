using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

/// <summary>
/// Strategy for tautology: a || !a => true, !a || a => true (pure)
/// </summary>
public class ConditionalOrTautologyStrategy : SymmetricStrategy<BooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalNotExpression } rightNot
		       && rightNot.Operand.IsEquivalentTo(context.Left.Syntax)
		       && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return SyntaxHelpers.CreateLiteral(true);
	}
}
