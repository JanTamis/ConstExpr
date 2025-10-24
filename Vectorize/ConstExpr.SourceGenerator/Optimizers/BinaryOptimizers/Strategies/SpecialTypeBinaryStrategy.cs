using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class SpecialTypeBinaryStrategy : BaseBinaryStrategy
{
	public abstract bool IsValidSpecialType(SpecialType specialType);

	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return IsValidSpecialType(context.Type.SpecialType);
	}
}
