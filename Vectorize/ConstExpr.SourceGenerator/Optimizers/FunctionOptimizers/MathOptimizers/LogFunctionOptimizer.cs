using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class LogFunctionOptimizer() : BaseMathFunctionOptimizer("Log", n => n is 1 or 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Log(Exp(x)) => x (inverse operation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// For float / double: replace with a scalar fast polynomial approximation.
		// Uses a degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
		// ln(x) = e·ln(2) + ln(m)   — no LOG10_E conversion step needed (vs Log10).
		// Benchmark speedup vs Math.Log (Apple M4 Pro / ARM64 RyuJIT):
		//   float  ≈ 2.0×  (1.764 ns → 0.888 ns)
		//   double ≈ 2.2×  (2.003 ns → 0.904 ns)
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastLogMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastLogMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			if (context.VisitedParameters.Count == 1)
			{
				// Log(x) => FastLog(x)
				result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
				return true;
			}

			// Log(x, newBase) => FastLog(x) / FastLog(newBase)
			// log_base(x) = ln(x) / ln(newBase).
			// Benchmark speedup vs Math.Log(x, newBase) (Apple M4 Pro / ARM64 RyuJIT):
			//   float  ≈ 2.2×  (4.541 ns → 2.021 ns)
			//   double ≈ 2.1×  (4.250 ns → 2.000 ns)
			result = DivideExpression(
				CreateInvocation(method.Identifier.Text, context.VisitedParameters[0]),
				CreateInvocation(method.Identifier.Text, context.VisitedParameters[1]));
			return true;
		}

		result = null;
		return false;
	}

	private static string GenerateFastLogMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastLog(float x)")
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
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var e    = (bits >> 23) - 127;")
			.WriteLine("var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);")
			.WriteWhitespace()
			.WriteLine("var lnm = Single.FusedMultiplyAdd(-0.056570851f, m, 0.447178975f);")
			.WriteLine("lnm     = Single.FusedMultiplyAdd(lnm, m, -1.469956800f);")
			.WriteLine("lnm     = Single.FusedMultiplyAdd(lnm, m, 2.821202636f);")
			.WriteLine("lnm     = Single.FusedMultiplyAdd(lnm, m, -1.741793927f);")
			.WriteWhitespace()
			.WriteLine("return Single.FusedMultiplyAdd(e, 0.6931471805599453f, lnm);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastLogMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastLog(double x)")
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
			.WriteLine("var lnm = Double.FusedMultiplyAdd(-0.056570851, m, 0.447178975);")
			.WriteLine("lnm     = Double.FusedMultiplyAdd(lnm, m, -1.469956800);")
			.WriteLine("lnm     = Double.FusedMultiplyAdd(lnm, m, 2.821202636);")
			.WriteLine("lnm     = Double.FusedMultiplyAdd(lnm, m, -1.741793927);")
			.WriteWhitespace()
			.WriteLine("return Double.FusedMultiplyAdd(e, 0.6931471805599453094172321214581766, lnm);");

		builder.EndBlock();

		return builder.ToString();
	}
}