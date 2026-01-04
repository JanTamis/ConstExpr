using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for idempotent division: x / x = 1 (pure expressions)
/// </summary>
public class DivideIdempotencyStrategy : NumericBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		return base.TryOptimize(context, out optimized)
		       && LeftEqualsRight(context)
		       && IsPure(context.Left.Syntax)
		       && SyntaxHelpers.TryGetLiteral(1.ToSpecialType(context.Left.Type!.SpecialType), out optimized);
	}
}