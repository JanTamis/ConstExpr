using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class ReciprocalEstimateFunctionOptimizer() : BaseMathFunctionOptimizer("ReciprocalEstimate", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Default: re-target to the numeric-type static method.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

