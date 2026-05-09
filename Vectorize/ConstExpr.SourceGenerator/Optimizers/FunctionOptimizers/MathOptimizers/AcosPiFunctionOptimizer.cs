using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AcosPiFunctionOptimizer() : BaseMathFunctionOptimizer("AcosPi", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAcosPiMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAcosPiMethodDouble(context.FastMathFlags),
			_ => null,
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation("FastAcosPi", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastAcosPiMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastAcosPi(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var negative = x < 0f;")
			.WriteLine("x = Single.Abs(x);")
			.WriteLine("if (x > 1.0f) x = 1.0f;")
			.WriteLine("")
			.WriteLine("// A&S §4.4.45 minimax polynomial (degree-3) coefficients pre-divided by π.")
			.WriteLine("// 3 FMAs + 1 sqrt. Max absolute error ≈ 5.4e-6 (vs 2.2e-4 for degree-2 §4.4.44).")
			.WriteLine("var p = Single.FusedMultiplyAdd(-0.00596227f, x, 0.02363378f);  // -0.0187293 / π, 0.0742610 / π")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x, -0.06751894f);                // -0.2121144 / π")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x, 0.5f);                        // π/2 / π = 0.5")
			.WriteLine("p *= Single.Sqrt(1f - x);")
			.WriteLine("")
			.WriteLine("// acosPi(-x) = 1 - acosPi(x)")
			.WriteLine("return negative ? 1f - p : p;");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastAcosPiMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastAcosPi(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("var negative = x < 0.0;")
			.WriteLine("x = Double.Abs(x);")
			.WriteLine("if (x > 1.0) x = 1.0;")
			.WriteLine("")
			.WriteLine("// A&S §4.4.45 minimax polynomial (degree-3) coefficients pre-divided by π.")
			.WriteLine("// Max absolute error ≈ 1.3e-6 (in units of π).")
			.WriteLine("var p = Double.FusedMultiplyAdd(-0.0059622704862860465, x, 0.023633778501171472);  // -0.0187293 / π, 0.0742610 / π")
			.WriteLine("p = Double.FusedMultiplyAdd(p, x, -0.067518943563376579);  // -0.2121144 / π")
			.WriteLine("p = Double.FusedMultiplyAdd(p, x, 0.5);                    // π/2 / π = 0.5")
			.WriteLine("p *= Double.Sqrt(1.0 - x);")
			.WriteLine("")
			.WriteLine("// acosPi(-x) = 1 - acosPi(x)")
			.WriteLine("return negative ? 1.0 - p : p;");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}