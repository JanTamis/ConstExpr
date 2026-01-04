using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class NumericOrBooleanBinaryStrategy<TLeft, TRight> : SpecialTypeBinaryStrategy<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is
			SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
			SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
			SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or SpecialType.System_Boolean;
	}
}

public class NumericOrBooleanBinaryStrategy : NumericOrBooleanBinaryStrategy<ExpressionSyntax, ExpressionSyntax>;