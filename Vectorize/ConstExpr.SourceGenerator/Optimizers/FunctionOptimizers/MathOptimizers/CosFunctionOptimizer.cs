using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CosFunctionOptimizer() : BaseMathFunctionOptimizer("Cos", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastCosMethodFloat()
				: GenerateFastCosMethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastCos", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastCosMethodFloat()
	{
		return """
			private static float FastCos(float x)
			{
				// Fast cosine approximation using minimax polynomial
				// Branchless range reduction to [-π, π]:
				// Round(x/τ) compiles to a single FRINTN (ARM64) / ROUNDSS (x64) —
				// avoids FDIV and conditional branches of the Floor-based approach.
				if (Single.IsNaN(x)) return Single.NaN;
				x -= Single.Round(x * (1f / Single.Tau)) * Single.Tau;
				
				// Use symmetry: cos(-x) = cos(x): fold to [0, π]
				x = Single.Abs(x);
				
				// Degree-8 minimax polynomial for cos(x) on [0, π], evaluated in x² (4 FMA)
				var x2 = x * x;
				var ret = 0.0003538394f;                                        // x^8 term
				ret = Single.FusedMultiplyAdd(ret, x2, -0.0041666418f);        // x^6 term
				ret = Single.FusedMultiplyAdd(ret, x2,  0.041666666f);         // x^4 term
				ret = Single.FusedMultiplyAdd(ret, x2, -0.5f);                 // x^2 term
				ret = Single.FusedMultiplyAdd(ret, x2,  1.0f);                 // constant term
				
				return ret;
			}
			""";
	}

	private static string GenerateFastCosMethodDouble()
	{
		return """
			private static double FastCos(double x)
			{
				// Fast cosine approximation using minimax polynomial
				// Branchless range reduction to [-π, π]:
				// Round(x/τ) compiles to a single FRINTA (ARM64) / ROUNDSD (x64) —
				// avoids FDIV and conditional branches of the Floor-based approach.
				if (Double.IsNaN(x)) return Double.NaN;
				x -= Double.Round(x * (1.0 / Double.Tau)) * Double.Tau;
				
				// Use symmetry: cos(-x) = cos(x): fold to [0, π]
				x = Double.Abs(x);
				
				// Degree-10 minimax polynomial for cos(x) on [0, π], evaluated in x² (5 FMA)
				var x2 = x * x;
				var ret = -1.1940250944959890e-7;                                         // x^10 term
				ret = Double.FusedMultiplyAdd(ret, x2,  2.0876755527587203e-5);           // x^8 term
				ret = Double.FusedMultiplyAdd(ret, x2, -0.0013888888888739916);           // x^6 term
				ret = Double.FusedMultiplyAdd(ret, x2,  0.041666666666666602);            // x^4 term
				ret = Double.FusedMultiplyAdd(ret, x2, -0.5);                             // x^2 term
				ret = Double.FusedMultiplyAdd(ret, x2,  1.0);                             // constant term
				
				return ret;
			}
			""";
	}
}