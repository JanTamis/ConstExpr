using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class IntegerBinaryStrategy : SpecialTypeBinaryStrategy
{
	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		throw new System.NotImplementedException();
	}

	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is SpecialType.System_SByte
			or SpecialType.System_Byte
			or SpecialType.System_Int16
			or SpecialType.System_UInt16
			or SpecialType.System_Int32
			or SpecialType.System_UInt32
			or SpecialType.System_Int64
			or SpecialType.System_UInt64;
	}
}
