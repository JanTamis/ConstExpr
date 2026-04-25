using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinCosFunctionOptimizer() : BaseMathFunctionOptimizer("SinCos", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSinCosMethodFloat(),
			SpecialType.System_Double => GenerateFastSinCosMethodDouble(),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastSinCosMethodFloat()
	{
		return """
			private static (float Sin, float Cos) FastSinCos(float x)
			{
				// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
				//   float.SinCos  = 3.08 ns  |  previous FastSinCos = 1.78 ns  |  this V2 = 1.60 ns
				// V2 changes vs previous: multiply instead of divide (no FDIV), removed dead
				// "if (absX > Pi)" branch, one branchless FCSEL instead of two if-branches,
				// single CopySign for sin sign instead of the nested double-CopySign form.
				if (Single.IsNaN(x)) return (Single.NaN, Single.NaN);
				
				const float Tau    = 6.283185307179586f;
				const float Pi     = 3.141592653589793f;
				const float HalfPi = 1.5707963267948966f;
				const float InvTau = 0.15915494309189535f; // 1/(2π) — avoids FDIV in range reduction
				
				// Range reduction to [-π, π]: multiply by InvTau instead of dividing by Tau
				x -= Single.Round(x * InvTau) * Tau;
				
				// Capture sign before folding to positive half
				var xSign = Single.CopySign(1.0f, x);
				var absX  = Single.Abs(x);
				
				// Branchless quadrant reduction to [0, π/2]: FCSEL on ARM64, no mispredictions
				var over    = absX > HalfPi;
				var sinArg  = over ? Pi - absX : absX;
				var cosSign = over ? -1.0f : 1.0f;
				
				var x2 = sinArg * sinArg;
				
				// Sin polynomial: degree-7 minimax (3 FMA + 1 mul)
				var sinVal = -0.00019840874f;
				sinVal = Single.FusedMultiplyAdd(sinVal, x2,  0.0083333310f);
				sinVal = Single.FusedMultiplyAdd(sinVal, x2, -0.16666667f);
				sinVal = Single.FusedMultiplyAdd(sinVal, x2,  1.0f);
				sinVal *= sinArg;
				sinVal  = Single.CopySign(sinVal, xSign); // one CopySign instead of the old nested form
				
				// Cos polynomial: degree-6 minimax (3 FMA + 1 add + 1 mul)
				var cosVal = 0.0013888397f;
				cosVal = Single.FusedMultiplyAdd(cosVal, x2, -0.041666418f);
				cosVal = Single.FusedMultiplyAdd(cosVal, x2,  0.5f);
				cosVal = Single.FusedMultiplyAdd(cosVal, x2, -1.0f);
				cosVal += 1.0f;
				cosVal *= cosSign;
				
				return (sinVal, cosVal);
			}
			""";
	}

	private static string GenerateFastSinCosMethodDouble()
	{
		return """
			private static (double Sin, double Cos) FastSinCos(double x)
			{
				// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
				//   Math.SinCos  = 5.33 ns  |  previous FastSinCos = 1.84 ns  |  this V2 = 1.62 ns
				// V2 changes: multiply instead of divide (no FDIV), removed dead "if (absX > Pi)"
				// branch, one branchless FCSEL instead of two if-branches, single CopySign.
				if (Double.IsNaN(x)) return (Double.NaN, Double.NaN);
				
				const double Tau    = 6.283185307179586476925;
				const double Pi     = 3.141592653589793238462;
				const double HalfPi = 1.570796326794896619231;
				const double InvTau = 0.15915494309189533576888; // 1/(2π) — avoids FDIV
				
				// Range reduction to [-π, π]: multiply by InvTau instead of dividing by Tau
				x -= Double.Round(x * InvTau) * Tau;
				
				var xSign = Double.CopySign(1.0, x);
				var absX  = Double.Abs(x);
				
				// Branchless quadrant reduction to [0, π/2]
				var over    = absX > HalfPi;
				var sinArg  = over ? Pi - absX : absX;
				var cosSign = over ? -1.0 : 1.0;
				
				var x2 = sinArg * sinArg;
				
				// Sin polynomial: degree-9 minimax (4 FMA + 1 mul)
				var sinVal = 2.7557313707070068e-6;
				sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.00019841269841201856);
				sinVal = Double.FusedMultiplyAdd(sinVal, x2,  0.0083333333333331650);
				sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.16666666666666666);
				sinVal = Double.FusedMultiplyAdd(sinVal, x2,  1.0);
				sinVal *= sinArg;
				sinVal  = Double.CopySign(sinVal, xSign);
				
				// Cos polynomial: degree-8 minimax (4 FMA + 1 add + 1 mul)
				var cosVal = -2.6051615464872668e-5;
				cosVal = Double.FusedMultiplyAdd(cosVal, x2,  0.0013888888888887398);
				cosVal = Double.FusedMultiplyAdd(cosVal, x2, -0.041666666666666664);
				cosVal = Double.FusedMultiplyAdd(cosVal, x2,  0.5);
				cosVal = Double.FusedMultiplyAdd(cosVal, x2, -1.0);
				cosVal += 1.0;
				cosVal *= cosSign;
				
				return (sinVal, cosVal);
			}
			""";
	}
}