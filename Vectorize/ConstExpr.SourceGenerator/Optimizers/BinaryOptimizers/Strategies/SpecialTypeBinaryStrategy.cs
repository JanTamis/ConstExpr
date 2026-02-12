using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class SpecialTypeBinaryStrategy<TLeft, TRight>(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : BaseBinaryStrategy<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public abstract bool IsValidSpecialType(SpecialType specialType);

	public override bool TryOptimize(BinaryOptimizeContext<TLeft, TRight> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		return (context.Type is not null && IsValidSpecialType(context.Type.SpecialType)
		        || context.Left.Type is not null && IsValidSpecialType(context.Left.Type.SpecialType)
		        || context.Right.Type is not null && IsValidSpecialType(context.Right.Type.SpecialType))
		       && (leftKind == SyntaxKind.None || context.Left.Syntax.IsKind(leftKind))
		       && (rightKind == SyntaxKind.None || context.Right.Syntax.IsKind(rightKind));
	}
}

public abstract class SpecialTypeBinaryStrategy(SyntaxKind leftKind = SyntaxKind.None, SyntaxKind rightKind = SyntaxKind.None) : SpecialTypeBinaryStrategy<ExpressionSyntax, ExpressionSyntax>(leftKind, rightKind)
{
	public SpecialTypeBinaryStrategy() : this(SyntaxKind.None)
	{

	}
}