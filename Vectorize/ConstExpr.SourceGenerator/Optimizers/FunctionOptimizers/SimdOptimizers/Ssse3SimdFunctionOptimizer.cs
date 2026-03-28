using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

/// <summary>SSSE3 intrinsics: Abs and Shuffle map to the same names on Vector128 — handled by base class fallback.</summary>
public class Ssse3SimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Ssse3")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;
		return false;
	}
}

