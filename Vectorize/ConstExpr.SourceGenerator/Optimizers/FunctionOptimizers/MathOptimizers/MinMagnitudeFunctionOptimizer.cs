using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class MinMagnitudeFunctionOptimizer() : BaseMathFunctionOptimizer("MinMagnitude", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var left = context.VisitedParameters[0];
		var right = context.VisitedParameters[1];

		// Idempotency: MinMagnitude(x, x) → x
		if (left.IsEquivalentTo(right) && IsPure(left))
		{
			result = left;
			return true;
		}

		// Re-target to the numeric-type static method (float.MinMagnitude / double.MinMagnitude).
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

