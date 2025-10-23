using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class BooleanBinaryStrategy : SpecialTypeBinaryStrategy
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is SpecialType.System_Boolean;
	}
}
