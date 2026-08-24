using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
///   Strategy for negated subtraction optimization:
///   x + (-y) => x - y (pure)
///   -x + y => y - x (pure)
///   Safe under Strict (pure algebraic identity).
/// </summary>
public class AddNegatedSubtractionStrategy() : SymmetricStrategy<NumericBinaryStrategy, ExpressionSyntax, PrefixUnaryExpressionSyntax>(rightKind: SyntaxKind.UnaryMinusExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}

		optimized = SubtractExpression(context.Left.Syntax, UnwrapRedundantParentheses(context.Right.Syntax.Operand));
		return true;
	}

	// Only safe to drop the grouping when the inner expression binds at least as tightly as
	// multiplication (unary, primary, or */% ) — anything additive-or-looser (e.g. `-(a + b)`,
	// `-(a - b)`) changes meaning if the parens are dropped from a subtraction's right operand.
	private static ExpressionSyntax UnwrapRedundantParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax { Expression: var inner }
		       && (inner is not BinaryExpressionSyntax binary
		           || binary.IsKind(SyntaxKind.MultiplyExpression)
		           || binary.IsKind(SyntaxKind.DivideExpression)
		           || binary.IsKind(SyntaxKind.ModuloExpression)))
		{
			expression = inner;
		}

		return expression;
	}
}