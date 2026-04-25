using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class MinMagnitudeNumberFunctionOptimizer() : BaseMathFunctionOptimizer("MinMagnitudeNumber", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var left = context.VisitedParameters[0];
		var right = context.VisitedParameters[1];

		// Idempotency: MinMagnitudeNumber(x, x) → x
		if (left.IsEquivalentTo(right) && IsPure(left))
		{
			result = left;
			return true;
		}

		// Re-target to the numeric-type static method (float.MinMagnitudeNumber / double.MinMagnitudeNumber).
		// MinMagnitudeNumber propagates NaN differently: it returns the non-NaN argument
		// when exactly one operand is NaN (IEEE 754-2019 minNumMag semantics).
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

