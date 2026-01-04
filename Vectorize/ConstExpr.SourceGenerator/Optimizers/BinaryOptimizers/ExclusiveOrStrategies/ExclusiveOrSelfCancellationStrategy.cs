using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for self XOR: x ^ x = 0 (pure)
/// </summary>
public class ExclusiveOrSelfCancellationStrategy : NumericBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !LeftEqualsRight(context)
		    || !IsPure(context.Left.Syntax))
			return false;
		
		optimized = SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		return true;
	}
}
