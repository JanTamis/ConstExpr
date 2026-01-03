using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// Strategy for boolean literal comparison: x == true => x, x == false => !x
/// </summary>
public class EqualsBooleanLiteralStrategy : BooleanBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Right.Syntax, out var value)
		    || value is not bool boolValue)
			return false;
		
		optimized = boolValue
				? context.Left.Syntax // x == true => x
				: PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(context.Left.Syntax)); // x == false => !x
		
		return true;
	}
}
