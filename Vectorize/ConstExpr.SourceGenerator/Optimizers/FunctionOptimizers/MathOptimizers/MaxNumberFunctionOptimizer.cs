using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class MaxNumberFunctionOptimizer() : BaseMathFunctionOptimizer("MaxNumber", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var left = context.VisitedParameters[0];
		var right = context.VisitedParameters[1];

		// Idempotency: MaxNumber(x, x) → x
		if (left.IsEquivalentTo(right) && IsPure(left))
		{
			result = left;
			return true;
		}

		// Re-target to the numeric-type static method (float.MaxNumber / double.MaxNumber).
		// MaxNumber follows IEEE 754-2019 maxNum semantics: NaN is treated as a missing
		// value, so it returns the non-NaN argument when exactly one operand is NaN.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

