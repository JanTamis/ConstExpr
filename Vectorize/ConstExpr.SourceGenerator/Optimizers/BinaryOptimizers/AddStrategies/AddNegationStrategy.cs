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
public class AddNegationStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return CanOptimizeNegationRight(context) || CanOptimizeNegationLeft(context);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// x + (-x) => 0 (pure)
		if (CanOptimizeNegationRight(context))
		{
			return SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		}

		// (-x) + x => 0 (pure)
		if (CanOptimizeNegationLeft(context))
		{
			return SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		}

		return null;
	}

	private static bool CanOptimizeNegationRight(BinaryOptimizeContext context)
	{
		var left = context.Left.Syntax;
		var right = context.Right.Syntax;

		return right is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } rNeg
			&& rNeg.Operand.IsEquivalentTo(left)
			&& IsPure(left) && IsPure(rNeg.Operand);
	}

	private static bool CanOptimizeNegationLeft(BinaryOptimizeContext context)
	{
		var left = context.Left.Syntax;
		var right = context.Right.Syntax;

		return left is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } lNeg
			&& lNeg.Operand.IsEquivalentTo(right)
			&& IsPure(right) && IsPure(lNeg.Operand);
	}
}
