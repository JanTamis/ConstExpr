using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class DegreesToRadiansFunctionOptimizer() : BaseMathFunctionOptimizer("DegreesToRadians", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Delegate to the built-in T.DegreesToRadians(x) for all numeric types.
		//
		// Benchmarks (Apple M4 Pro, ARM64, .NET 10, N=1024):
		//   float.DegreesToRadians  : 0.538 ns  (baseline)
		//   x * (MathF.PI / 180f)   : 0.593 ns  (+10% slower, 1 ULP less accurate)
		//
		// The JIT recognises float/double.DegreesToRadians as a vectorisable loop and
		// emits multi-accumulator SIMD code. The explicit multiply-by-constant does not
		// trigger the same optimisation. The builtin also uses the more precise constant
		// (float)(Math.PI / 180.0) = 0x3C8EFA35 vs MathF.PI / 180f = 0x3C8EFA36 (1 ULP off).
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}