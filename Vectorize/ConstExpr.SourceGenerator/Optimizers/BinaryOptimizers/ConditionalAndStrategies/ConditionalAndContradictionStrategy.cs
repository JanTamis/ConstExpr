using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for contradiction: a && !a => false and !a && a => false (pure)
/// </summary>
public class ConditionalAndContradictionStrategy : BooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		// a && !a => false (contradiction, pure)
		if (context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } rightNot
				&& rightNot.Operand.IsEquivalentTo(context.Left.Syntax)
				&& IsPure(context.Left.Syntax))
		{
			return true;
		}

		// !a && a => false (contradiction, pure)
		if (context.Left.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } leftNot
				&& leftNot.Operand.IsEquivalentTo(context.Right.Syntax)
				&& IsPure(context.Right.Syntax))
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return SyntaxHelpers.CreateLiteral(false);
	}
}
