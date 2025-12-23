using System;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class LiteralBinaryStrategy : SpecialTypeBinaryStrategy
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return IsLiteral(specialType);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		throw new NotImplementedException();
	}

	public static bool IsLiteral(SpecialType specialType)
	{
		return specialType is SpecialType.System_Boolean
			or SpecialType.System_Char
			or SpecialType.System_String
			or SpecialType.System_Byte
			or SpecialType.System_Double
			or SpecialType.System_Single
			or SpecialType.System_Decimal
			or SpecialType.System_Int16
			or SpecialType.System_Int32
			or SpecialType.System_Int64
			or SpecialType.System_SByte
			or SpecialType.System_UInt16
			or SpecialType.System_UInt32
			or SpecialType.System_UInt64;
	}
}