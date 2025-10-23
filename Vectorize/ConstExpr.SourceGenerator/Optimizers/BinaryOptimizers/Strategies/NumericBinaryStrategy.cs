﻿using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies
{
	public abstract class NumericBinaryStrategy : SpecialTypeBinaryStrategy
	{
		public override bool IsValidSpecialType(SpecialType specialType)
		{
			return specialType is
				SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
				SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
				SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
		}
	}
}
