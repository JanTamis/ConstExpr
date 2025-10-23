using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for contradiction: a && !a => false and !a && a => false (pure)
/// </summary>
public class ConditionalAndContradictionStrategy : SymmetricStrategy<BooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalNotExpression } rightNot
		       && rightNot.Operand.IsEquivalentTo(context.Left.Syntax)
		       && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return SyntaxHelpers.CreateLiteral(false);
	}
}
