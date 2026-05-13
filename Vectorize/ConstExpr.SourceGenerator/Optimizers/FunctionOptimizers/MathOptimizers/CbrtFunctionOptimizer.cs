using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CbrtFunctionOptimizer() : BaseMathFunctionOptimizer("Cbrt", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastCbrtMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastCbrtMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastCbrtMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastCbrt(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x == 0.0f) return 0.0f;")
			.WriteWhitespace()
			.WriteLine("var absX = Single.Abs(x);")
			.WriteWhitespace()
			// .WriteLine("// Initial approximation using bit manipulation (~7 significant bits)")
			.WriteLine("var i = BitConverter.SingleToInt32Bits(absX);")
			.WriteLine("i = 0x2a517d47 + i / 3;")
			.WriteLine("var y = BitConverter.Int32BitsToSingle(i);")
			.WriteWhitespace()
			// .WriteLine("// Single Halley iteration: y = y * (y³ + 2a) / (2y³ + a)")
			// .WriteLine("// Cubic convergence: 7 bits → ~21 bits in one step (vs two Newton steps for ~20 bits).")
			// .WriteLine("// One division instead of two — benchmarked at ~2× faster than the 2×Newton approach.")
			.WriteLine("var y2 = y * y;")
			.WriteLine("var y3 = y2 * y;")
			.WriteLine("var twoA = absX + absX;")
			.WriteLine("y = y * Single.FusedMultiplyAdd(1.0f, y3, twoA) / Single.FusedMultiplyAdd(2.0f, y3, absX);")
			.WriteWhitespace()
			.WriteLine("return Single.CopySign(y, x);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCbrtMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastCbrt(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x == 0.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine("var absX = Double.Abs(x);")
			.WriteWhitespace()
			// .WriteLine("// Initial approximation using bit manipulation (~8 significant bits)")
			.WriteLine("var i = BitConverter.DoubleToInt64Bits(absX);")
			.WriteLine("i = 0x2a9f8b7cef1d0da0L + i / 3;")
			.WriteLine("var y = BitConverter.Int64BitsToDouble(i);")
			.WriteWhitespace()
			// .WriteLine("// 1× Newton: y = (2y + a/y²) / 3  — reaches ~16 bits")
			.WriteLine("y = (y + y + absX / (y * y)) / 3.0;")
			.WriteWhitespace()
			// .WriteLine("// 1× Halley: y = y * (y³ + 2a) / (2y³ + a)")
			// .WriteLine("// Cubic convergence from 16 bits → ~48 bits (vs 2×Newton which only reached ~32 bits).")
			// .WriteLine("// Same two-division cost as the previous 2×Newton implementation.")
			.WriteLine("var y2 = y * y;")
			.WriteLine("var y3 = y2 * y;")
			.WriteLine("var twoA = absX + absX;")
			.WriteLine("y = y * Double.FusedMultiplyAdd(1.0, y3, twoA) / Double.FusedMultiplyAdd(2.0, y3, absX);")
			.WriteWhitespace()
			.WriteLine("return Double.CopySign(y, x);");

		builder.EndBlock();

		return builder.ToString();
	}
}