using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for double-to-shift optimization: x + x => x << 1 (integer, pure)
/// </summary>
public class AddDoubleToShiftStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return LeftEqualsRight(context) && IsPure(context.Left.Syntax) && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return BinaryExpression(SyntaxKind.LeftShiftExpression, context.Left.Syntax, SyntaxHelpers.CreateLiteral(1));
	}
}
