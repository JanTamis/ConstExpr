using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

/// <summary>
/// Optimizer for Math.Ceiling / MathF.Ceiling.
///
/// Benchmark findings (Apple M4 Pro, .NET 10, ARM64 — CeilingBenchmark):
///   Math.Ceiling      → 0.561 ns  (single FRINTP instruction) ← optimal
///   double.Ceiling    → 0.570 ns  (+1.6% — same instruction via IFloatingPoint&lt;T&gt;)
///   -Math.Floor(-x)   → 0.581 ns  (+3.6% — avoid; extra negation adds overhead)
///   long-cast trick   → 0.667 ns  (+18.9% — never use; FP/int domain crossing is costly)
///
/// The algebraic identity Ceiling(-x) = -Floor(x) was previously emitted for unary-minus
/// arguments. Benchmarks confirm it yields no throughput benefit (both paths cost 2 FP ops on
/// ARM64: a negation plus a round instruction). The rewrite has been removed to keep the
/// generated code simple and to avoid the marginal latency of an extra negation in loop contexts.
/// </summary>
public class CeilingFunctionOptimizer() : BaseMathFunctionOptimizer("Ceiling",n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Idempotence: Ceiling(Ceiling(x)) → Ceiling(x)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Ceiling" } } innerInv)
		{
			result = innerInv;
			return true;
		}

		// Default: emit Ceiling call directly — already the optimal scalar implementation.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}
