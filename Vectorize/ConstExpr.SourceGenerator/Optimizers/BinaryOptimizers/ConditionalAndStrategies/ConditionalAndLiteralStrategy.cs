using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for literal boolean optimization: false && x = false, true && x = x
/// </summary>
public class ConditionalAndLiteralStrategy : SymmetricStrategy<BooleanBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = context.Left.Syntax.Token.Value switch
		{
			// false && x = false
			false => CreateLiteral(false),
			// true && x = x
			true => context.Right.Syntax,
			_ => null,
		};
			
		return optimized != null;
	}
}
