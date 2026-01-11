using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for boolean true absorption: x | true = true and true | x = true
/// </summary>
public class OrBooleanTrueStrategy : SymmetricStrategy<BooleanBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Left.Syntax.Token.Value is not true)
		{
			optimized = null;
			return false;
		}

		optimized = SyntaxHelpers.CreateLiteral(true);
		return true;
	}
}
