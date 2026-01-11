using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Absorption with Or: x & (x | y) = x and (x | y) & x = x (pure)
/// </summary>
public class AndAbsorptionStrategy() : SymmetricStrategy<NumericOrBooleanBinaryStrategy, ExpressionSyntax, BinaryExpressionSyntax>(rightKind: SyntaxKind.BitwiseOrExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context.Left.Syntax, context.Right.Syntax.Left, context.Variables)
		    || !LeftEqualsRight(context.Left.Syntax, context.Right.Syntax.Right, context.Variables)
		    || !IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}
		
		optimized = context.Left.Syntax;
		return true;
	}
}
