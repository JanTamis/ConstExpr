using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for subtraction cancellation optimization:
/// (x - a) + a => x (algebraic identity, pure)
/// a + (x - a) => x (algebraic identity, pure)
/// </summary>
public class AddSubtractionCancellationStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.SubtractExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context.Left.Syntax.Right, context.Right.Syntax, context.Variables)
		    || !IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}
		
		optimized = context.Left.Syntax.Left;
		return true;
	}
}