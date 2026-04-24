using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

/// <summary>
/// Optimises <c>Math.Sqrt</c> / <c>MathF.Sqrt</c> call sites.
///
/// <b>General case — keep the built-in.</b>
/// Both float and double square root map to a single hardware instruction
/// (<c>fsqrt</c> on ARM64, <c>SQRTSS</c>/<c>SQRTSD</c> on x86).  Benchmarks on
/// Apple M4 Pro (.NET 10, ARM64) show:
/// <list type="bullet">
///   <item><description><c>MathF.Sqrt</c>  — 0.486 ns/call  ← optimal</description></item>
///   <item><description>ReciprocalSqrtEstimate + Newton — 1.007 ns  (2.1× slower)</description></item>
///   <item><description>Bit-hack + 2× Newton — 1.241 ns  (2.6× slower)</description></item>
///   <item><description><c>Math.Sqrt</c>   — 0.477 ns/call  ← optimal</description></item>
///   <item><description>Float-seed + 2× Newton — 1.543 ns  (3.2× slower)</description></item>
///   <item><description>Bit-hack + 3× Newton — 2.061 ns  (4.3× slower)</description></item>
/// </list>
/// No scalar software approximation can beat the hardware instruction.
///
/// <b>Algebraic identity — <c>Sqrt(x * x)</c> → <c>Abs(x)</c>.</b>
/// When the argument is literally <c>e * e</c> for a pure sub-expression <c>e</c>, the
/// result equals <c>|e|</c>.  On ARM64 the .NET JIT already folds this pattern at
/// machine-code level (measured ~0.50 ns for both forms), but on x86 the rewrite
/// saves the entire <c>SQRTSS</c> latency (~10–14 cycles vs 1 cycle for <c>FABS</c>),
/// and it exposes further source-level optimisation opportunities.
/// </summary>
public class SqrtFunctionOptimizer() : BaseMathFunctionOptimizer("Sqrt", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Sqrt(x * x) => Abs(x)
		// Algebraic identity: sqrt(e²) = |e| for any pure expression e.
		// Saves the entire SQRTSS/fsqrt on x86; on ARM64 the JIT folds this anyway,
		// but the rewrite improves source clarity and enables further optimisations.
		if (arg is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression } mul
		    && mul.Left.IsEquivalentTo(mul.Right)
		    && IsPure(mul.Left))
		{
			var mathType = ParseTypeName(paramType.Name);
			result = InvocationExpression(
					MemberAccessExpression(mathType, IdentifierName("Abs")))
				.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(mul.Left))));
			return true;
		}

		// General case: emit the built-in Math.Sqrt / MathF.Sqrt.
		// Hardware fsqrt is ~0.48 ns/call on ARM64 — no software approximation beats it.
		result = CreateInvocation(paramType, "Sqrt", arg);
		return true;
	}
}