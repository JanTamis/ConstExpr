using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class SpecialTypeBinaryStrategy<TLeft, TRight> : BaseBinaryStrategy<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public abstract bool IsValidSpecialType(SpecialType specialType);

	public override bool TryOptimize(BinaryOptimizeContext<TLeft, TRight> context, out ExpressionSyntax? optimized)
	{
		optimized = null;
		
		return IsValidSpecialType(context.Type.SpecialType)
		       || context.Left.Type is not null && IsValidSpecialType(context.Left.Type.SpecialType)
		       || context.Right.Type is not null && IsValidSpecialType(context.Right.Type.SpecialType);
	}
}

public abstract class SpecialTypeBinaryStrategy : SpecialTypeBinaryStrategy<ExpressionSyntax, ExpressionSyntax>;