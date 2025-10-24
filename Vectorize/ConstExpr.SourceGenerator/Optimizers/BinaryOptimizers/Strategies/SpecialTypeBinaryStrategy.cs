using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class SpecialTypeBinaryStrategy : BaseBinaryStrategy
{
	public abstract bool IsValidSpecialType(SpecialType specialType);

	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return IsValidSpecialType(context.Type.SpecialType)
		       || context.Left.Type is not null && IsValidSpecialType(context.Left.Type.SpecialType)
		       || context.Right.Type is not null && IsValidSpecialType(context.Right.Type.SpecialType);
	}
}