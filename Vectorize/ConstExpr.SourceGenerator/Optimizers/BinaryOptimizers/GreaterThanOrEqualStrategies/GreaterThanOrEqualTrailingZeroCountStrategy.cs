using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanOrEqualStrategies;

/// <summary>
///   Strategy for divisibility-by-power-of-two tests via TrailingZeroCount:
///   <c>TrailingZeroCount(x) &gt;= c</c> => <c>(x &amp; ((1 &lt;&lt; c) - 1)) == 0</c>.
///   Holds for signed and unsigned types, including <c>x == 0</c> (TrailingZeroCount
///   returns the bit width there and the mask test is also true). Safe under Strict.
/// </summary>
public class GreaterThanOrEqualTrailingZeroCountStrategy : BaseBinaryStrategy
{
	private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
			expression = parenthesized.Expression;
		return expression;
	}

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (UnwrapParentheses(context.Left.Syntax) is not InvocationExpressionSyntax invocation
		    || invocation.Expression is not MemberAccessExpressionSyntax member
		    || member.Name.Identifier.Text != "TrailingZeroCount"
		    || invocation.ArgumentList.Arguments.Count != 1
		    || !context.TryGetValue(context.Right.Syntax, out var countValue)
		    || countValue is not int count)
		{
			return false;
		}

		var argument = invocation.ArgumentList.Arguments[0].Expression;

		if (context.TryGetValue(argument, out _) // fully constant: folds upstream
		    || !context.Model.TryGetTypeSymbol(argument, context.SymbolStore, out var argumentType))
		{
			return false;
		}

		// count == 0 is a tautology and count >= the bit width means x == 0; both are left to other passes
		var mask = (argumentType.SpecialType, count) switch
		{
			(SpecialType.System_Int32, > 0 and < 32) => (object) ((1 << count) - 1),
			(SpecialType.System_UInt32, > 0 and < 32) => (1u << count) - 1,
			(SpecialType.System_Int64, > 0 and < 64) => (1L << count) - 1,
			(SpecialType.System_UInt64, > 0 and < 64) => (1ul << count) - 1,
			_ => null
		};

		if (mask is null || !TryCreateLiteral(mask, out var maskLiteral))
		{
			return false;
		}

		optimized = EqualsExpression(
			ParenthesizedExpression(BitwiseAndExpression(argument, maskLiteral)),
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
		return true;
	}
}