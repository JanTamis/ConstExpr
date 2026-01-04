using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class FloatNumberBinaryStrategy<TLeft, TRight> : SpecialTypeBinaryStrategy<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is SpecialType.System_Single
			or SpecialType.System_Double
			or SpecialType.System_Decimal;
	}
}

public class FloatNumberBinaryStrategy : FloatNumberBinaryStrategy<ExpressionSyntax, ExpressionSyntax>;