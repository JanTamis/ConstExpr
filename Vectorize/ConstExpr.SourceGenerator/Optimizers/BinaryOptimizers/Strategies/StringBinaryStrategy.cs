using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class StringBinaryStrategy<TLeft, TRight>(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : SpecialTypeBinaryStrategy<TLeft, TRight>(leftKind, rightKind)
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is SpecialType.System_String;
	}
}

public class StringBinaryStrategy(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : StringBinaryStrategy<ExpressionSyntax, ExpressionSyntax>(leftKind, rightKind)
{
	public StringBinaryStrategy() : this(SyntaxKind.None)
	{
		
	}
}