using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class UnsigedIntegerBinaryStrategy<TLeft, TRight>(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : SpecialTypeBinaryStrategy<TLeft, TRight>(leftKind, rightKind)
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is SpecialType.System_Byte
			or SpecialType.System_UInt16
			or SpecialType.System_UInt32
			or SpecialType.System_UInt64;
	}
}

public class UnsigedIntegerBinaryStrategy(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : IntegerBinaryStrategy<ExpressionSyntax, ExpressionSyntax>(leftKind, rightKind)
{
	public UnsigedIntegerBinaryStrategy() : this(SyntaxKind.None)
	{
		
	}
}