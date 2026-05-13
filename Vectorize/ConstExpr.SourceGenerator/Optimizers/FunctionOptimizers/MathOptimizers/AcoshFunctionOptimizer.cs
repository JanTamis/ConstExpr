using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AcoshFunctionOptimizer() : BaseMathFunctionOptimizer("Acosh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAcoshMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAcoshMethodDouble(context.FastMathFlags),
			_ => null,
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

	private static string GenerateFastAcoshMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastAcosh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x < 1.0f) x = 1.0f;")
			.WriteWhitespace()
			.WriteLine("if (x > 1e7f)")
			.StartBlock()
			.WriteLine("return Single.Log(2.0f * x);")
			.EndBlock()
			.WriteWhitespace()
			// .WriteLine("// For values close to 1, use polynomial approximation with FMA to avoid log.")
			// .WriteLine("// Taylor series: acosh(1+t)/sqrt(2t) = 1 − t/12 + 3t²/160 − …")
			// .WriteLine("// Horner form:   1 + t*(−1/12 + t*(3/160)) = FMA(t, FMA(t, 3/160, −1/12), 1)")
			.WriteLine("if (x < 1.5f)")
			.StartBlock()
			.WriteLine("var t = x - 1.0f;")
			.WriteLine("var sqrt2t = Single.Sqrt(2.0f * t);")
			.WriteLine("var correction = Single.FusedMultiplyAdd(t, Single.FusedMultiplyAdd(t, 0.01875f, -0.0833333f), 1.0f);")
			.WriteLine("return sqrt2t * correction;")
			.EndBlock()
			.WriteWhitespace()
			// .WriteLine("// Use FMA: sqrt(x^2 - 1)")
			.WriteLine("var sqrtTerm = Single.Sqrt(Single.FusedMultiplyAdd(x, x, -1.0f));")
			.WriteLine("return Single.Log(x + sqrtTerm);")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAcoshMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastAcosh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x < 1.0) x = 1.0;")
			.WriteWhitespace()
			.WriteLine("if (x > 1e15)")
			.StartBlock()
			.WriteLine("return Double.Log(2.0 * x);")
			.EndBlock()
			.WriteWhitespace()
			// .WriteLine("// For values close to 1, use polynomial approximation with FMA to avoid log.")
			// .WriteLine("// Taylor series: acosh(1+t)/sqrt(2t) = 1 − t/12 + 3t²/160 − 5t³/896 − …")
			// .WriteLine("// Horner form: FMA(t, FMA(t, FMA(t, −5/896, 3/160), −1/12), 1.0)")
			.WriteLine("if (x < 1.5)")
			.StartBlock()
			.WriteLine("var t = x - 1.0;")
			.WriteLine("var sqrt2t = Double.Sqrt(2.0 * t);")
			.WriteLine("var correction = Double.FusedMultiplyAdd(t, Double.FusedMultiplyAdd(t, Double.FusedMultiplyAdd(t, -0.005580357, 0.01875), -0.083333333333), 1.0);")
			.WriteLine("return sqrt2t * correction;")
			.EndBlock()
			.WriteWhitespace()
			// .WriteLine("// Use FMA: sqrt(x^2 - 1)")
			.WriteLine("var sqrtTerm = Double.Sqrt(Double.FusedMultiplyAdd(x, x, -1.0));")
			.WriteLine("return Double.Log(x + sqrtTerm);")
			.EndBlock();

		return builder.ToString();
	}
}