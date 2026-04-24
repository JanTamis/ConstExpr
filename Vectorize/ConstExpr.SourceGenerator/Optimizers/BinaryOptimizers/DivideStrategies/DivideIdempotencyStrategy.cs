using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for idempotent division: x / x = 1 (pure expressions)
/// Safe under Strict (pure algebraic identity).
/// </summary>
public class DivideIdempotencyStrategy : NumericBinaryStrategy
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		return base.TryOptimize(context, out optimized)
		       && LeftEqualsRight(context)
		       && IsPure(context.Left.Syntax)
		       && TryCreateLiteral(1.ToSpecialType(context.Left.Type!.SpecialType), out optimized);
	}
}