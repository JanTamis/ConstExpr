using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class TruncateFunctionOptimizer() : BaseMathFunctionOptimizer("Truncate", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// 1) Idempotence: Truncate(Truncate(x)) -> Truncate(x)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Truncate" } } innerInv)
		{
			result = innerInv;
			return true;
		}

		// 2) Integer types: Truncate(x) -> x (truncate has no effect on integers)
		if (paramType.IsNonFloatingNumeric())
		{
			result = context.VisitedParameters[0];
			return true;
		}

		// 3) Unary minus: Truncate(-x) -> -Truncate(x)
		if (context.VisitedParameters[0] is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix)
		{
			var truncateCall = CreateInvocation(paramType, "Truncate", prefix.Operand);

			result = UnaryMinusExpression(ParenthesizedExpression(truncateCall));
			return true;
		}

		// 4) Truncate(Floor(x)) -> Floor(x) (Floor already truncates towards negative infinity)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Floor" } } floorInv)
		{
			result = floorInv;
			return true;
		}

		// 5) Truncate(Ceiling(x)) -> Ceiling(x) (Ceiling already returns an integer)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Ceiling" } } ceilingInv)
		{
			result = ceilingInv;
			return true;
		}

		// 6) Truncate(Round(x)) -> Round(x) (Round already returns an integer value)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Round" } } roundInv)
		{
			result = roundInv;
			return true;
		}

		// Default: float.Truncate / double.Truncate / Math.Truncate — all lower to a single FRINTZ/ROUNDSS
		// hardware instruction. The former FastTruncate bit-manipulation was 16–24% slower on ARM64
		// (see TruncateBenchmark). Let the JIT emit the optimal instruction directly.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}
