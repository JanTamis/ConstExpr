using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Log10FunctionOptimizer() : BaseMathFunctionOptimizer("Log10", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Log10(Exp10(x)) => x  (inverse-operation cancellation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp10" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// For float / double: replace with a scalar fast polynomial approximation.
		// FastLog10V2 uses a degree-4 Horner polynomial whose coefficients are the
		// ln(m) minimax coefficients pre-multiplied by log10(e), eliminating the
		// final lnm * LOG10_E multiply.  Benchmark speedup vs Math.Log10:
		//   float  ≈ 2.0×  (1.782 ns → 0.897 ns, Apple M4 Pro / ARM64 RyuJIT)
		//   double ≈ 2.3×  (2.020 ns → 0.892 ns)
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastLog10MethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastLog10MethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: delegate to the numeric-helper type's Log10.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastLog10MethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastLog10(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x) || x < 0f) return Single.NaN;");
		}

		builder.WriteLine("if (x == 0f) return Single.NegativeInfinity;");

		if (!flags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;");
		}

		builder.WriteWhitespace()
			// .WriteLine("// Bit-extract base-2 exponent e and mantissa m ∈ [1, 2).")
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var e    = (bits >> 23) - 127;")
			.WriteLine("var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);")
			.WriteWhitespace()
			// .WriteLine("// Degree-4 Horner polynomial for log10(m), m ∈ [1, 2).")
			// .WriteLine("// Coefficients d_i = c_i * log10(e) are the ln(m) minimax coefficients")
			// .WriteLine("// pre-multiplied by log10(e) = 1/ln(10), saving one post-multiplication.")
			// .WriteLine("// Max relative error ≈ 8.7e-5 (fast-math trade-off).")
			// .WriteLine("const float d4 = -0.024568408f;  // c4 * log10(e)")
			// .WriteLine("const float d3 =  0.194207361f;  // c3 * log10(e)")
			// .WriteLine("const float d2 = -0.638394127f;  // c2 * log10(e)")
			// .WriteLine("const float d1 =  1.225232737f;  // c1 * log10(e)")
			// .WriteLine("const float d0 = -0.756451491f;  // c0 * log10(e)")
			// .WriteWhitespace()
			.WriteLine("var log10m = Single.FusedMultiplyAdd(-0.024568408f, m, 0.194207361f);")
			.WriteLine("log10m     = Single.FusedMultiplyAdd(log10m, m, -0.638394127f);")
			.WriteLine("log10m     = Single.FusedMultiplyAdd(log10m, m, 1.225232737f);")
			.WriteLine("log10m     = Single.FusedMultiplyAdd(log10m, m, -0.756451491f);")
			.WriteWhitespace()
			.WriteLine("return Single.FusedMultiplyAdd(e, 0.30102999566398120f, log10m);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastLog10MethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastLog10(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x) || x < 0.0) return Double.NaN;");
		}

		builder.WriteLine("if (x == 0.0) return Double.NegativeInfinity;");

		if (!flags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;");
		}

		builder.WriteWhitespace()
			// .WriteLine("// Bit-extract base-2 exponent e and mantissa m ∈ [1, 2).")
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("var e    = (int)((bits >> 52) - 1023L);")
			.WriteLine("var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);")
			.WriteWhitespace()
			// .WriteLine("// Degree-4 Horner polynomial for log10(m), m ∈ [1, 2).")
			// .WriteLine("// Coefficients d_i = c_i * log10(e) are the ln(m) minimax coefficients")
			// .WriteLine("// pre-multiplied by log10(e) = 1/ln(10), saving one post-multiplication.")
			// .WriteLine("// Max relative error ≈ 8.7e-5 (fast-math trade-off).")
			// .WriteLine("const double d4 = -0.024568408426;  // c4 * log10(e)")
			// .WriteLine("const double d3 =  0.194207361266;  // c3 * log10(e)")
			// .WriteLine("const double d2 = -0.638394126876;  // c2 * log10(e)")
			// .WriteLine("const double d1 =  1.225232737146;  // c1 * log10(e)")
			// .WriteLine("const double d0 = -0.756451491109;  // c0 * log10(e)")
			// .WriteWhitespace()
			.WriteLine("var log10m = Double.FusedMultiplyAdd(-0.024568408426, m, 0.194207361266);")
			.WriteLine("log10m     = Double.FusedMultiplyAdd(log10m, m, -0.638394126876);")
			.WriteLine("log10m     = Double.FusedMultiplyAdd(log10m, m, 1.225232737146);")
			.WriteLine("log10m     = Double.FusedMultiplyAdd(log10m, m, -0.756451491109);")
			.WriteWhitespace()
			.WriteLine("return Double.FusedMultiplyAdd(e, 0.30102999566398119521373889472449303, log10m);");

		builder.EndBlock();

		return builder.ToString();
	}
}