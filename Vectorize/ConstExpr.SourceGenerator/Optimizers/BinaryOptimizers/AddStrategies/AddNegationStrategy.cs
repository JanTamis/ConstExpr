using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for negation cancellation: x + (-x) => 0 and (-x) + x => 0
/// </summary>
public class AddNegationStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		var left = context.Left.Syntax;
		var right = context.Right.Syntax;

		return right is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } rNeg
		       && rNeg.Operand.IsEquivalentTo(left)
		       && IsPure(left) && IsPure(rNeg.Operand);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
	}
}