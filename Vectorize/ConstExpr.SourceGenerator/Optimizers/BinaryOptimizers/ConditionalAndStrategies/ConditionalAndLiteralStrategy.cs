using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for literal boolean optimization: false && x = false, true && x = x
/// </summary>
public class ConditionalAndLiteralStrategy : BooleanBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
			return false;
		
		optimized = context.Left.Syntax.Token.Value switch
		{
			// false && x = false
			false => SyntaxHelpers.CreateLiteral(false),
			// true && x = x
			true => context.Right.Syntax,
			_ => null,
		};
			
		return true;
	}
}
