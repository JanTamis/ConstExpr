using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
///   Strategy for shift-based zero tests on unsigned integers:
///   <c>(x &gt;&gt; c) == 0</c> => <c>x &lt; 2^c</c> (one comparison, no shift).
///   Unsigned only: for signed types a negative <c>x</c> keeps its sign bit through
///   the arithmetic shift, so the equivalence does not hold. Safe under Strict.
/// </summary>
public class EqualsRightShiftZeroStrategy()
	: SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.RightShiftExpression)
{
	private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
			expression = parenthesized.Expression;
		return expression;
	}

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// (x >> c) is always parenthesized next to ==/!= ('>>' binds looser), so unwrap first
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

		if (!context.Right.Syntax.IsNumericZero()
		    || context.TryGetValue(context.Left.Syntax.Left, out _) // fully constant: folds elsewhere
		    || !context.TryGetValue(context.Left.Syntax.Right, out var shiftValue))
		{
			return false;
		}

		// shift counts of 0 are stripped by the shift strategies; counts >= the bit width are masked by C#
		var boundary = (context.Left.Type?.SpecialType, shiftValue) switch
		{
			(SpecialType.System_UInt32, int c and > 0 and < 32) => (object) (1u << c),
			(SpecialType.System_UInt64, int c and > 0 and < 64) => 1ul << c,
			_ => null
		};

		if (boundary is null || !TryCreateLiteral(boundary, out var boundaryLiteral))
		{
			return false;
		}

		optimized = CreateComparison(context.Left.Syntax.Left, boundaryLiteral);
		return true;
	}

	/// <summary>(x &gt;&gt; c) == 0 => x &lt; 2^c; the != mirror overrides this to x &gt;= 2^c.</summary>
	protected virtual ExpressionSyntax CreateComparison(ExpressionSyntax operand, ExpressionSyntax boundary)
	{
		return LessThanExpression(operand, boundary);
	}
}