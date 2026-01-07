using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for contradiction: a && !a => false and !a && a => false (pure)
/// </summary>
public class ConditionalAndContradictionStrategy : SymmetricStrategy<BooleanBinaryStrategy, ExpressionSyntax, PrefixUnaryExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsKind(SyntaxKind.LogicalNotExpression)
		    || !LeftEqualsRight(context.Right.Syntax.Operand, context.Left.Syntax, context.TryGetValue)
		    || !IsPure(context.Left.Syntax))
		{
			optimized = null;
			return false;
		}
		
		optimized = SyntaxHelpers.CreateLiteral(false);
		return true;
	}
}
