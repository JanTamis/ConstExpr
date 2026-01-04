using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class CharBinaryStrategy<TLeft, TRight> : SpecialTypeBinaryStrategy<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType == SpecialType.System_Char;
	}
}

public class CharBinaryStrategy : CharBinaryStrategy<ExpressionSyntax, ExpressionSyntax>;