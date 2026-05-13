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
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return (Single.NaN, Single.NaN);");
		}

		builder // .WriteLine("// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):")
			// .WriteLine("//   float.SinCos  = 3.08 ns  |  previous FastSinCos = 1.78 ns  |  this V2 = 1.60 ns")
			// .WriteLine("// V2 changes vs previous: multiply instead of divide (no FDIV), removed dead")
			// .WriteLine("// \"if (absX > Pi)\" branch, one branchless FCSEL instead of two if-branches,")
			// .WriteLine("// single CopySign for sin sign instead of the nested double-CopySign form.")
			.WriteWhitespace()
			// .WriteLine("// Range reduction to [-π, π]: multiply by InvTau instead of dividing by Tau")
			.WriteLine("x -= Single.Round(x * 0.15915494309189535f) * Single.Tau;")
			.WriteWhitespace()
			// .WriteLine("// Capture sign before folding to positive half")
			.WriteLine("var xSign = Single.CopySign(1.0f, x);")
			.WriteLine("var absX  = Single.Abs(x);")
			.WriteWhitespace()
			// .WriteLine("// Branchless quadrant reduction to [0, π/2]: FCSEL on ARM64, no mispredictions")
			.WriteLine("var over    = absX > 1.5707963267948966f;")
			.WriteLine("var sinArg  = over ? Single.Pi - absX : absX;")
			.WriteLine("var cosSign = over ? -1.0f : 1.0f;")
			.WriteWhitespace()
			.WriteLine("var x2 = sinArg * sinArg;")
			.WriteWhitespace()
			// .WriteLine("// Sin polynomial: degree-7 minimax (3 FMA + 1 mul)")
			.WriteLine("var sinVal = -0.00019840874f;")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, x2,  0.0083333310f);")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, x2, -0.16666667f);")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, x2,  1.0f);")
			.WriteLine("sinVal *= sinArg;")
			.WriteLine("sinVal  = Single.CopySign(sinVal, xSign);")
			.WriteWhitespace()
			// .WriteLine("// Cos polynomial: degree-6 minimax (3 FMA + 1 add + 1 mul)")
			.WriteLine("var cosVal = 0.0013888397f;")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, x2, -0.041666418f);")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, x2,  0.5f);")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, x2, -1.0f);")
			.WriteLine("cosVal += 1.0f;")
			.WriteLine("cosVal *= cosSign;")
			.WriteWhitespace()
			.WriteLine("return (sinVal, cosVal);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinCosMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static (double Sin, double Cos) FastSinCos(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return (Double.NaN, Double.NaN);");
		}

		builder // .WriteLine("// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):")
			// .WriteLine("//   Math.SinCos  = 5.33 ns  |  previous FastSinCos = 1.84 ns  |  this V2 = 1.62 ns")
			// .WriteLine("// V2 changes: multiply instead of divide (no FDIV), removed dead \"if (absX > Pi)\"")
			// .WriteLine("// branch, one branchless FCSEL instead of two if-branches, single CopySign.")
			.WriteWhitespace()
			// .WriteLine("// Range reduction to [-π, π]: multiply by InvTau instead of dividing by Tau")
			.WriteLine("x -= Double.Round(x * 0.15915494309189533576888) * Double.Tau;")
			.WriteWhitespace()
			.WriteLine("var xSign = Double.CopySign(1.0, x);")
			.WriteLine("var absX  = Double.Abs(x);")
			.WriteWhitespace()
			// .WriteLine("// Branchless quadrant reduction to [0, π/2]")
			.WriteLine("var over    = absX > 1.570796326794896619231;")
			.WriteLine("var sinArg  = over ? Double.Pi - absX : absX;")
			.WriteLine("var cosSign = over ? -1.0 : 1.0;")
			.WriteWhitespace()
			.WriteLine("var x2 = sinArg * sinArg;")
			.WriteWhitespace()
			// .WriteLine("// Sin polynomial: degree-9 minimax (4 FMA + 1 mul)")
			.WriteLine("var sinVal = 2.7557313707070068e-6;")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.00019841269841201856);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, x2,  0.0083333333333331650);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.16666666666666666);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, x2,  1.0);")
			.WriteLine("sinVal *= sinArg;")
			.WriteLine("sinVal  = Double.CopySign(sinVal, xSign);")
			.WriteWhitespace()
			// .WriteLine("// Cos polynomial: degree-8 minimax (4 FMA + 1 add + 1 mul)")
			.WriteLine("var cosVal = -2.6051615464872668e-5;")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, x2,  0.0013888888888887398);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, x2, -0.041666666666666664);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, x2,  0.5);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, x2, -1.0);")
			.WriteLine("cosVal += 1.0;")
			.WriteLine("cosVal *= cosSign;")
			.WriteWhitespace()
			.WriteLine("return (sinVal, cosVal);");

		builder.EndBlock();

		return builder.ToString();
	}
}