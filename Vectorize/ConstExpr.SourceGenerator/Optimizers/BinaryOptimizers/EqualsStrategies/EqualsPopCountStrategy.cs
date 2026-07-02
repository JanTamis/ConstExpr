using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
///   Strategy for PopCount comparisons:
///   <c>PopCount(x) == 0</c> => <c>x == 0</c> (any integer type) and
///   <c>PopCount(x) == 1</c> => <c>IsPow2(x)</c> (unsigned only: for signed types the sign
///   bit alone has PopCount 1 but is not a power of two). Safe under Strict.
/// </summary>
public class EqualsPopCountStrategy : SymmetricStrategy<NumericBinaryStrategy, InvocationExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<InvocationExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (context.Left.Syntax.Expression is not MemberAccessExpressionSyntax member
		    || member.Name.Identifier.Text != "PopCount"
		    || context.Left.Syntax.ArgumentList.Arguments.Count != 1
		    || !context.TryGetValue(context.Right.Syntax, out var countValue))
		{
			return false;
		}

		var argument = context.Left.Syntax.ArgumentList.Arguments[0].Expression;

		if (context.TryGetValue(argument, out _)) // fully constant: folds upstream
		{
			return false;
		}

		if (countValue.IsNumericZero())
		{
			optimized = CreateZeroComparison(argument);
			return true;
		}

		if (countValue.IsNumericOne()
		    && context.Model.TryGetTypeSymbol(argument, context.SymbolStore, out var argumentType)
		    && argumentType.IsUnsignedInteger())
		{
			optimized = CreatePow2Result(
				InvocationExpression(member.WithName(IdentifierName("IsPow2")))
					.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(argument)))));
			return true;
		}

		return false;
	}

	/// <summary>PopCount(x) == 0 => x == 0; the != mirror overrides to x != 0.</summary>
	protected virtual ExpressionSyntax CreateZeroComparison(ExpressionSyntax operand)
	{
		return EqualsExpression(operand, LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
	}

	/// <summary>PopCount(x) == 1 => IsPow2(x); the != mirror wraps it in <c>!</c>.</summary>
	protected virtual ExpressionSyntax CreatePow2Result(ExpressionSyntax isPow2)
	{
		return isPow2;
	}
}