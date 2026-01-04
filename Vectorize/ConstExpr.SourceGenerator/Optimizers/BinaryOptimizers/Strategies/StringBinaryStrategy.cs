using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class StringBinaryStrategy<TLeft, TRight> : SpecialTypeBinaryStrategy<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is SpecialType.System_String;
	}
}

public class StringBinaryStrategy : StringBinaryStrategy<ExpressionSyntax, ExpressionSyntax>;