using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

/// <summary>FMA intrinsics: all methods have different names (MultiplyAdd → FusedMultiplyAdd, etc.) and take 3 parameters.</summary>
public class FmaSimdFunctionOptimizer() : BaseSimdFunctionOptimizer("Fma")
{
	public override bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		switch (context.Method.Parameters.Length)
		{
			case 3:
			{
				switch (context.Method.Name)
				{
					case "MultiplyAdd":
					{
						result = CreateSimdInvocation(context, vectorType, "FusedMultiplyAdd", context.VisitedParameters);
						return true;
					}
					// a*b - c  →  FusedMultiplyAdd(a, b, -c)
					case "MultiplySubtract":
					{
						result = CreateSimdInvocation(context, vectorType, "FusedMultiplyAdd",
						[
							context.VisitedParameters[0],
							context.VisitedParameters[1],
							UnaryMinusExpression(context.VisitedParameters[2])
						]);
						return true;
					}
					// -(a*b) + c  →  FusedMultiplyAdd(-a, b, c)
					case "MultiplyAddNegated":
					{
						result = CreateSimdInvocation(context, vectorType, "FusedMultiplyAdd",
						[
							UnaryMinusExpression(context.VisitedParameters[0]),
							context.VisitedParameters[1],
							context.VisitedParameters[2]
						]);
						return true;
					}
					// -(a*b) - c  →  FusedMultiplyAdd(-a, b, -c)
					case "MultiplySubtractNegated":
					{
						result = CreateSimdInvocation(context, vectorType, "FusedMultiplyAdd",
						[
							UnaryMinusExpression(context.VisitedParameters[0]),
							context.VisitedParameters[1],
							UnaryMinusExpression(context.VisitedParameters[2])
						]);
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
