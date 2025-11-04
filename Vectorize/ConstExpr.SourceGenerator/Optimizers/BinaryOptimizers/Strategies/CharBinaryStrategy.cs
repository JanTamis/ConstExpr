using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class CharBinaryStrategy : SpecialTypeBinaryStrategy
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType == SpecialType.System_Char;
	}
}