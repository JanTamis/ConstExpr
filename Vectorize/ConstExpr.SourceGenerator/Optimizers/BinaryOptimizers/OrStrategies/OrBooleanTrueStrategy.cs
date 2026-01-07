using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for boolean true absorption: x | true = true and true | x = true
/// </summary>
public class OrBooleanTrueStrategy : BooleanBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
			return false;

		if (context.TryGetValue(context.Left.Syntax, out var leftValue)
		    && leftValue is true || context.TryGetValue(context.Right.Syntax, out var rightValue)
		    && rightValue is true)
		{
			optimized = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		return false;
	}
}
