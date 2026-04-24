using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

/// <summary>SSE3 intrinsics: LoadAndDuplicate broadcasts a scalar to all lanes → Vector128.Create.</summary>
public class Sse3SimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Sse3")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 1:
			{
				switch (context.Method.Name)
				{
					// Broadcast scalar to all lanes
					case "LoadAndDuplicate":
					{
						result = CreateSimdInvocation(context, vectorType, "Create", context.VisitedParameters);
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