using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to shift: 2 * x => x << 1 (integer)
/// </summary>
public class MultiplyByTwoToShiftStrategy : SymmetricStrategy<UnsigedIntegerBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsNumericTwo())
		{
			optimized = null;
			return false;
		}
		
		optimized = BinaryExpression(SyntaxKind.LeftShiftExpression, 
			context.Right.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
		
		return true;
	}
}
