using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class BooleanBinaryStrategy<TLeft, TRight>(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : SpecialTypeBinaryStrategy<TLeft, TRight>(leftKind, rightKind)
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is SpecialType.System_Boolean;
	}
}

public class BooleanBinaryStrategy(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : BooleanBinaryStrategy<ExpressionSyntax, ExpressionSyntax>(leftKind, rightKind)
{
	public BooleanBinaryStrategy() : this(SyntaxKind.None)
	{
		
	}
}