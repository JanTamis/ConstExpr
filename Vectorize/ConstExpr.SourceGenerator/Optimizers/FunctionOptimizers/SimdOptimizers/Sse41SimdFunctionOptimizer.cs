using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

public class Sse41SimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Sse41")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 1:
			{
				switch (context.Method.Name)
				{
					// RoundToZero maps to Truncate
					case "RoundToZero":
					{
						result = CreateSimdInvocation(context, vectorType, "Truncate", context.VisitedParameters);
						return true;
					}
					// Rounding variants with different names
					case "RoundToNearestInteger":
					{
						result = CreateSimdInvocation(context, vectorType, "Round", context.VisitedParameters);
						return true;
					}
					case "RoundToNegativeInfinity":
					{
						result = CreateSimdInvocation(context, vectorType, "Floor", context.VisitedParameters);
						return true;
					}
					case "RoundToPositiveInfinity":
					{
						result = CreateSimdInvocation(context, vectorType, "Ceiling", context.VisitedParameters);
						return true;
					}
					// Non-temporal load with different name
					case "LoadAlignedVector128NonTemporal":
					{
						result = CreateSimdInvocation(context, vectorType, "LoadAlignedNonTemporal", context.VisitedParameters);
						return true;
					}
				}
				break;
			}
			case 2:
			{
				switch (context.Method.Name)
				{
					// PackUnsignedSaturate clamps on overflow → NarrowWithSaturation
					case "PackUnsignedSaturate":
					{
						result = CreateSimdInvocation(context, vectorType, "NarrowWithSaturation", context.VisitedParameters);
						return true;
					}
				}
				break;
			}
			case 3:
			{
				switch (context.Method.Name)
				{
					// BlendVariable(left, right, mask) → ConditionalSelect(mask, left, right)
					case "BlendVariable":
					{
						result = CreateSimdInvocation(context, vectorType, "ConditionalSelect", context.VisitedParameters);
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