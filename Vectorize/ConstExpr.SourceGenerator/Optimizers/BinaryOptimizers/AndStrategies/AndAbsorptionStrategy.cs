using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Absorption with Or: x & (x | y) = x and (x | y) & x = x (pure)
/// </summary>
public class AndAbsorptionStrategy : SymmetricStrategy<NumericOrBooleanBinaryStrategy, ExpressionSyntax, BinaryExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsKind(SyntaxKind.BitwiseOrExpression)
		    || !LeftEqualsRight(context.Left.Syntax, context.Right.Syntax.Left, context.TryGetValue)
		    || !LeftEqualsRight(context.Left.Syntax, context.Right.Syntax.Right, context.TryGetValue)
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
