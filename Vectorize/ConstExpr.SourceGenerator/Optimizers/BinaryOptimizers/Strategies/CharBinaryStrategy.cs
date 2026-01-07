using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class CharBinaryStrategy<TLeft, TRight>(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : SpecialTypeBinaryStrategy<TLeft, TRight>(leftKind, rightKind)
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType == SpecialType.System_Char;
	}
}

public class CharBinaryStrategy(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : CharBinaryStrategy<ExpressionSyntax, ExpressionSyntax>(leftKind, rightKind)
{
	public CharBinaryStrategy() : this(SyntaxKind.None, SyntaxKind.None)
	{
		
	}
}