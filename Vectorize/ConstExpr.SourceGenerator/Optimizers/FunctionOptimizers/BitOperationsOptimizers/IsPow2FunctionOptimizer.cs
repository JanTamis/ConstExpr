using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.BitOperationsOptimizers;

/// <summary>
///   Expands <c>BitOperations.IsPow2(x)</c> to an inline branchless expression.
///   For unsigned types: <c>x != 0u &amp;&amp; (x &amp; (x - 1u)) == 0u</c>
///   For signed types:   <c>x &gt; 0 &amp;&amp; (x &amp; (x - 1)) == 0</c>
///   Constant arguments are folded by <c>TryExecuteWithConstantArguments</c> upstream.
/// </summary>
public class IsPow2FunctionOptimizer() : BaseBitOperationsFunctionOptimizer("IsPow2", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		if (!IsPure(arg))
		{
			result = null;
			return false;
		}

		if (paramType.IsUnsignedInteger())
		{
			var zero = MakeUnsignedLiteral(paramType, 0);
			var one = MakeUnsignedLiteral(paramType, 1);

			// x != 0u && (x & (x - 1u)) == 0u
			var notZero = BinaryExpression(SyntaxKind.NotEqualsExpression, arg, zero);
			var xMinusOne = BinaryExpression(SyntaxKind.SubtractExpression, arg, one);
			var andExpr = BinaryExpression(SyntaxKind.BitwiseAndExpression, arg, ParenthesizedExpression(xMinusOne));
			var eqZero = BinaryExpression(SyntaxKind.EqualsExpression, ParenthesizedExpression(andExpr), zero);

			result = BinaryExpression(SyntaxKind.LogicalAndExpression, notZero, eqZero);
			return true;
		}

		if (paramType.IsInteger())
		{
			var zero = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
			var one = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1));

			// x > 0 && (x & (x - 1)) == 0
			var gtZero = BinaryExpression(SyntaxKind.GreaterThanExpression, arg, zero);
			var xMinusOne = BinaryExpression(SyntaxKind.SubtractExpression, arg, one);
			var andExpr = BinaryExpression(SyntaxKind.BitwiseAndExpression, arg, ParenthesizedExpression(xMinusOne));
			var eqZero = BinaryExpression(SyntaxKind.EqualsExpression, ParenthesizedExpression(andExpr), zero);

			result = BinaryExpression(SyntaxKind.LogicalAndExpression, gtZero, eqZero);
			return true;
		}

		result = null;
		return false;
	}

	private static LiteralExpressionSyntax MakeUnsignedLiteral(ITypeSymbol type, int value)
	{
		return type.SpecialType == SpecialType.System_UInt64
			? LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((ulong)value))
			: LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((uint)value));
	}
}