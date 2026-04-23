using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

/// <summary>
/// Optimizer for Math.Floor / MathF.Floor.
///
/// Benchmark findings (Apple M4 Pro, .NET 10, ARM64 — CeilingBenchmark, symmetric):
///   Math.Floor / Math.Ceiling  → ~0.561 ns  (single FRINTM/FRINTP instruction) ← optimal
///   -Math.Ceiling(-x)          → ~0.581 ns  (+3.6% — avoid; the extra negation adds overhead)
///
/// The algebraic identity Floor(-x) = -Ceiling(x) was previously emitted for unary-minus
/// arguments. Benchmarks on the symmetric Ceiling case confirm no throughput benefit (both
/// paths cost the same 2 FP ops on ARM64). The rewrite has been removed to keep generated
/// code simple.
/// </summary>
public class FloorFunctionOptimizer() : BaseMathFunctionOptimizer("Floor", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Idempotence: Floor(Floor(x)) → Floor(x)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Floor" } } innerInv)
		{
			result = innerInv;
			return true;
		}

		// Default: emit Floor call directly — already the optimal scalar implementation.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}