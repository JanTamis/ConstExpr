using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class MaxMagnitudeFunctionOptimizer() : BaseMathFunctionOptimizer("MaxMagnitude", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var left = context.VisitedParameters[0];
		var right = context.VisitedParameters[1];

		// Idempotency: MaxMagnitude(x, x) → x
		if (SyntaxNodeComparer.Get().Equals(left, right) && IsPure(left))
		{
			result = left;
			return true;
		}

		// Re-target to the numeric-type static method (float.MaxMagnitude / double.MaxMagnitude).
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

