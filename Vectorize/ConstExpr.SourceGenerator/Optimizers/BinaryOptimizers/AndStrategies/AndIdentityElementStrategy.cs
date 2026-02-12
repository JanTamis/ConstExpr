using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Identity element: x & 0 = 0 (for numeric types), x & true = x, x & false = false (for boolean type)
/// </summary>
public class AndIdentityElementStrategy : SymmetricStrategy<NumericOrBooleanBinaryStrategy, ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Right.Syntax.IsNumericZero())
		{
			optimized = context.Right.Syntax;
			return true;
		}
		
		switch (context.Right.Syntax.Token.Value)
		{
			case false:
				optimized = SyntaxHelpers.CreateLiteral(false);
				return true;
			case true:
				optimized = context.Left.Syntax;
				return true;
			default:
				optimized = null;
				return false;
		}
	}
}