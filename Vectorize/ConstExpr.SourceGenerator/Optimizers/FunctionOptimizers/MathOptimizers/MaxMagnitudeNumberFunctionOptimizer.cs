using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class MaxMagnitudeNumberFunctionOptimizer() : BaseMathFunctionOptimizer("MaxMagnitudeNumber", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var left = context.VisitedParameters[0];
		var right = context.VisitedParameters[1];

		// Idempotency: MaxMagnitudeNumber(x, x) → x
		if (SyntaxNodeComparer.Get().Equals(left, right) && IsPure(left))
		{
			result = left;
			return true;
		}

		// Re-target to the numeric-type static method (float.MaxMagnitudeNumber / double.MaxMagnitudeNumber).
		// MaxMagnitudeNumber propagates NaN differently from MaxMagnitude: it treats NaN as
		// a missing value and returns the non-NaN argument when exactly one operand is NaN.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

