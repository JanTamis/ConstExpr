using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for absorption law: a && (a || b) => a and (a || b) && a => a (pure)
/// </summary>
public class ConditionalAndAbsorptionStrategy() : SymmetricStrategy<BooleanBinaryStrategy, ExpressionSyntax, BinaryExpressionSyntax>(leftKind: SyntaxKind.LogicalOrExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax)
		    || !LeftEqualsRight(context.Right.Syntax.Left, context.Left.Syntax, context.Variables)
		    || !LeftEqualsRight(context.Right.Syntax.Right, context.Left.Syntax, context.Variables))
		{
			optimized = null;
			return false;
		}
		
		optimized = context.Left.Syntax;
		return true;
	}
}
