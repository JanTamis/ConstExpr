using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

/// <summary>SSE4.2 intrinsics: only CompareGreaterThan for 64-bit integers maps to GreaterThan.</summary>
public class Sse42SimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Sse42")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 2:
			{
				switch (context.Method.Name)
				{
					case "CompareGreaterThan":
					{
						result = CreateSimdInvocation(context, vectorType, "GreaterThan", context.VisitedParameters);
						return true;
					}
				}
				break;
			}
		}

		result = null;
		return false;
	}
}