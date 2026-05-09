using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinCosFunctionOptimizer() : BaseMathFunctionOptimizer("SinCos", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSinCosMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastSinCosMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastSinCosMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static (float Sin, float Cos) FastSinCos(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return (Single.NaN, Single.NaN);");
		}

		builder.WriteLine("// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):")
			.WriteLine("//   float.SinCos  = 3.08 ns  |  previous FastSinCos = 1.78 ns  |  this V2 = 1.60 ns")
			.WriteLine("// V2 changes vs previous: multiply instead of divide (no FDIV), removed dead")
			.WriteLine("// \"if (absX > Pi)\" branch, one branchless FCSEL instead of two if-branches,")
			.WriteLine("// single CopySign for sin sign instead of the nested double-CopySign form.")
			.WriteLine("")
			.WriteLine("const float Tau    = 6.283185307179586f;")
			.WriteLine("const float Pi     = 3.141592653589793f;")
			.WriteLine("const float HalfPi = 1.5707963267948966f;")
			.WriteLine("const float InvTau = 0.15915494309189535f; // 1/(2π) — avoids FDIV in range reduction")
			.WriteLine("")
			.WriteLine("// Range reduction to [-π, π]: multiply by InvTau instead of dividing by Tau")
			.WriteLine("x -= Single.Round(x * InvTau) * Tau;")
			.WriteLine("")
			.WriteLine("// Capture sign before folding to positive half")
			.WriteLine("var xSign = Single.CopySign(1.0f, x);")
			.WriteLine("var absX  = Single.Abs(x);")
			.WriteLine("")
			.WriteLine("// Branchless quadrant reduction to [0, π/2]: FCSEL on ARM64, no mispredictions")
			.WriteLine("var over    = absX > HalfPi;")
			.WriteLine("var sinArg  = over ? Pi - absX : absX;")
			.WriteLine("var cosSign = over ? -1.0f : 1.0f;")
			.WriteLine("")
			.WriteLine("var x2 = sinArg * sinArg;")
			.WriteLine("")
			.WriteLine("// Sin polynomial: degree-7 minimax (3 FMA + 1 mul)")
			.WriteLine("var sinVal = -0.00019840874f;")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, x2,  0.0083333310f);")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, x2, -0.16666667f);")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, x2,  1.0f);")
			.WriteLine("sinVal *= sinArg;")
			.WriteLine("sinVal  = Single.CopySign(sinVal, xSign); // one CopySign instead of the old nested form")
			.WriteLine("")
			.WriteLine("// Cos polynomial: degree-6 minimax (3 FMA + 1 add + 1 mul)")
			.WriteLine("var cosVal = 0.0013888397f;")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, x2, -0.041666418f);")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, x2,  0.5f);")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, x2, -1.0f);")
			.WriteLine("cosVal += 1.0f;")
			.WriteLine("cosVal *= cosSign;")
			.WriteLine("")
			.WriteLine("return (sinVal, cosVal);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastSinCosMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static (double Sin, double Cos) FastSinCos(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return (Double.NaN, Double.NaN);");
		}

		builder.WriteLine("// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):")
			.WriteLine("//   Math.SinCos  = 5.33 ns  |  previous FastSinCos = 1.84 ns  |  this V2 = 1.62 ns")
			.WriteLine("// V2 changes: multiply instead of divide (no FDIV), removed dead \"if (absX > Pi)\"")
			.WriteLine("// branch, one branchless FCSEL instead of two if-branches, single CopySign.")
			.WriteLine("")
			.WriteLine("const double Tau    = 6.283185307179586476925;")
			.WriteLine("const double Pi     = 3.141592653589793238462;")
			.WriteLine("const double HalfPi = 1.570796326794896619231;")
			.WriteLine("const double InvTau = 0.15915494309189533576888; // 1/(2π) — avoids FDIV")
			.WriteLine("")
			.WriteLine("// Range reduction to [-π, π]: multiply by InvTau instead of dividing by Tau")
			.WriteLine("x -= Double.Round(x * InvTau) * Tau;")
			.WriteLine("")
			.WriteLine("var xSign = Double.CopySign(1.0, x);")
			.WriteLine("var absX  = Double.Abs(x);")
			.WriteLine("")
			.WriteLine("// Branchless quadrant reduction to [0, π/2]")
			.WriteLine("var over    = absX > HalfPi;")
			.WriteLine("var sinArg  = over ? Pi - absX : absX;")
			.WriteLine("var cosSign = over ? -1.0 : 1.0;")
			.WriteLine("")
			.WriteLine("var x2 = sinArg * sinArg;")
			.WriteLine("")
			.WriteLine("// Sin polynomial: degree-9 minimax (4 FMA + 1 mul)")
			.WriteLine("var sinVal = 2.7557313707070068e-6;")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.00019841269841201856);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, x2,  0.0083333333333331650);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.16666666666666666);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, x2,  1.0);")
			.WriteLine("sinVal *= sinArg;")
			.WriteLine("sinVal  = Double.CopySign(sinVal, xSign);")
			.WriteLine("")
			.WriteLine("// Cos polynomial: degree-8 minimax (4 FMA + 1 add + 1 mul)")
			.WriteLine("var cosVal = -2.6051615464872668e-5;")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, x2,  0.0013888888888887398);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, x2, -0.041666666666666664);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, x2,  0.5);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, x2, -1.0);")
			.WriteLine("cosVal += 1.0;")
			.WriteLine("cosVal *= cosSign;")
			.WriteLine("")
			.WriteLine("return (sinVal, cosVal);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}