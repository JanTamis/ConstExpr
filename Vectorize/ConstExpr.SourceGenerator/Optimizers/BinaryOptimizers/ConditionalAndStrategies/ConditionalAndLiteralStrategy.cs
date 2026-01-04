using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for literal boolean optimization: false && x = false, true && x = x
/// </summary>
public class ConditionalAndLiteralStrategy : BooleanBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Left.Syntax, out var value)
		    || value is not bool)
			return false;
		
		optimized = value switch
		{
			// false && x = false
			false => SyntaxHelpers.CreateLiteral(false),
			// true && x = x
			true => context.Right.Syntax,
		};
			
		return true;
	}
}
