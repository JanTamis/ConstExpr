using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

/// <summary>
/// Strategy for tautology: a || !a => true, !a || a => true (pure)
/// </summary>
public class ConditionalOrTautologyStrategy : SymmetricStrategy<BooleanBinaryStrategy, ExpressionSyntax, PrefixUnaryExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsKind(SyntaxKind.LogicalNotExpression)
		    || !LeftEqualsRight(context.Right.Syntax.Operand, context.Left.Syntax, context.Variables)
		    || !IsPure(context.Left.Syntax))
		{
			optimized = null;
			return false;
		}
		
		optimized = SyntaxHelpers.CreateLiteral(true);
		return true;
	}
}
