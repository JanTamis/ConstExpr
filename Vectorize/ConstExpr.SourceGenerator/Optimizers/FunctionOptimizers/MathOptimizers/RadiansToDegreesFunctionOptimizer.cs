using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class RadiansToDegreesFunctionOptimizer() : BaseMathFunctionOptimizer("RadiansToDegrees", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Delegate to the built-in T.RadiansToDegrees(x) for all numeric types.
		//
		// Benchmarks (Apple M4 Pro, ARM64, .NET 10, N=1024):
		//   float.RadiansToDegrees  : 0.508 ns  (baseline)
		//   x * (180f / MathF.PI)   : 0.544 ns  (+7% slower)
		//   double.RadiansToDegrees : 0.495 ns  (baseline)
		//   x * (180.0 / Math.PI)   : 0.535 ns  (+8% slower)
		//
		// The JIT recognises float/double.RadiansToDegrees as a vectorisable loop and
		// emits multi-accumulator SIMD code. The explicit multiply-by-constant does not
		// trigger the same optimisation. The builtin also uses the more precise constant
		// (float)(180.0 / Math.PI) vs 180f / MathF.PI (may differ by 1 ULP).
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}