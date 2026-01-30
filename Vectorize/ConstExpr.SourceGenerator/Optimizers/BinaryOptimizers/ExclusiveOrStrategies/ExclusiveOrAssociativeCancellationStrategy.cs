using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for associative cancellation: (x ^ y) ^ x = y (pure)
/// </summary>
public class ExclusiveOrAssociativeCancellationStrategy()
	: SymmetricStrategy<NumericOrBooleanBinaryStrategy, BinaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.ExclusiveOrExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (IsPure(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}

		if (LeftEqualsRight(context.Right.Syntax, context.Left.Syntax.Left, context.Variables)
		    && IsPure(context.Left.Syntax.Left))
		{
			optimized = context.Left.Syntax.Right;
			return true;
		}

		if (LeftEqualsRight(context.Right.Syntax, context.Left.Syntax.Right, context.Variables)
		    && IsPure(context.Left.Syntax.Right))
		{
			optimized = context.Left.Syntax.Left;
			return true;
		}

		optimized = null;
		return false;
	}
}
