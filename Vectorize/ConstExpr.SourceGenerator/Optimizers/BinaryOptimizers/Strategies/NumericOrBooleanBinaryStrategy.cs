using System;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class NumericOrBooleanBinaryStrategy : SpecialTypeBinaryStrategy
{
	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		throw new NotImplementedException();
	}

	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is
			SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
			SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
			SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or SpecialType.System_Boolean;
	}
}