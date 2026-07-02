using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
///   Strategy for modulo-by-power-of-two zero-equality: (x % 2^n) == 0 => (x &amp; (2^n - 1)) == 0.
///   Unlike the general x % 2^n => x &amp; (2^n - 1) fold (see ModuloByPowerOfTwoStrategy), divisibility by
///   a power of two is sign-invariant (e.g. -4 % 4 == 0 and -4 &amp; 3 == 0; -5 % 4 == -1 and -5 &amp; 3 == 3),
///   so this holds for any integer type without a non-negativity proof.
/// </summary>
public class EqualsModuloPowerOfTwoZeroStrategy() : SymmetricStrategy<IntegerBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.ModuloExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
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
			Parent = context.Parent
		};
		return base.TryOptimize(unwrappedContext, out optimized);
	}

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!context.Right.Syntax.IsNumericZero()
		    || context.Left.Type is null
		    || !context.TryGetValue(context.Left.Syntax.Right, out var modValue)
		    || !modValue.IsNumericPowerOfTwo(out var power)
		    || !TryCreateLiteral(((1 << power) - 1).ToSpecialType(context.Left.Type.SpecialType), out var maskLiteral))
		{
			return false;
		}

		optimized = EqualsExpression(
			ParenthesizedExpression(BitwiseAndExpression(context.Left.Syntax.Left, maskLiteral)),
			context.Right.Syntax);

		return true;
	}

	private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
		{
			expression = parenthesized.Expression;
		}

		return expression;
	}
}