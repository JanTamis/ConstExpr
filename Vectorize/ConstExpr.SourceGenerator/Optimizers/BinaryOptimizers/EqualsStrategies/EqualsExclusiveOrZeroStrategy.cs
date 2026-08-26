using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
///   Strategy for XOR-zero equality tests: (a ^ b) == 0 => a == b.
///   Both operands of the XOR are evaluated exactly once, in the same left-to-right order,
///   on both sides of the rewrite, so no purity guard is needed. Safe under Strict.
/// </summary>
public class EqualsExclusiveOrZeroStrategy()
	: SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.ExclusiveOrExpression)
{
	private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
			expression = parenthesized.Expression;
		return expression;
	}

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// (a ^ b) is always parenthesized next to ==/!= ('^' binds looser), so unwrap first
		var leftUnwrapped = UnwrapParentheses(context.Left.Syntax);
		var rightUnwrapped = UnwrapParentheses(context.Right.Syntax);

		if (leftUnwrapped == context.Left.Syntax && rightUnwrapped == context.Right.Syntax)
		{
			return base.TryOptimize(context, out optimized);
		}

		var unwrappedContext = new BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax>
		{
			Left = new BinaryOptimizeElement<ExpressionSyntax> { Syntax = leftUnwrapped, Type = context.Left.Type },
			Right = new BinaryOptimizeElement<ExpressionSyntax> { Syntax = rightUnwrapped, Type = context.Right.Type },
			Type = context.Type,
			Variables = context.Variables,
			TryGetValue = context.TryGetValue,
			BinaryExpressions = context.BinaryExpressions,
			Parent = context.Parent,
			Model = context.Model,
			SymbolStore = context.SymbolStore
		};

		return base.TryOptimize(unwrappedContext, out optimized);
	}

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!context.Right.Syntax.IsNumericZero())
		{
			return false;
		}

		optimized = CreateComparison(context.Left.Syntax.Left, context.Left.Syntax.Right);
		return true;
	}

	/// <summary>(a ^ b) == 0 => a == b; the != mirror overrides this to a != b.</summary>
	protected virtual ExpressionSyntax CreateComparison(ExpressionSyntax left, ExpressionSyntax right)
	{
		return EqualsExpression(left, right);
	}
}