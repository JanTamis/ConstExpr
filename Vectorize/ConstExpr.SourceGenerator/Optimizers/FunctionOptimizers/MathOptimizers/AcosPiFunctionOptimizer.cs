using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AcosPiFunctionOptimizer() : BaseMathFunctionOptimizer("AcosPi", n => n is 1)
{
	/// <summary>
	///   Attempts to optimize a Math.AcosPi function call by generating a fast approximation implementation.
	/// </summary>
	/// <param name="context">The optimizer context containing method arguments and FastMath flags.</param>
	/// <param name="paramType">The type symbol of the parameter (float or double).</param>
	/// <param name="result">The optimized syntax node if successful; otherwise null.</param>
	/// <returns>True if optimization was successful; otherwise false.</returns>
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

	/// <summary>
	///   Generates a fast approximation implementation of the inverse cosine divided by π (AcosPi) function for
	///   single-precision floating-point numbers.
	/// </summary>
	/// <param name="flags">FastMath flags that control NaN handling and other optimizations.</param>
	/// <returns>A string containing the C# code for the fast AcosPi implementation.</returns>
	private static string GenerateFastAcosPiMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast polynomial approximation of inverse cosine divided by π (AcosPi) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Returns Acos(x) / π. Uses polynomial approximation with FusedMultiplyAdd. Handles negative values and optional NaN checks.</remarks>")
			.WriteLine("""/// <param name="x">Input value in the range [-1, 1].</param>""")
			.WriteLine("/// <returns>Approximate inverse cosine value divided by π, in the range [0, 1].</returns>")
			.WriteLine("private static float FastAcosPi(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var negative = x < 0f;")
			.WriteLine("x = Single.Abs(x);")
			.WriteLine("if (x > 1.0f) x = 1.0f;")
			.WriteWhitespace()
			.WriteLine("var p = Single.FusedMultiplyAdd(-0.00596227f, x, 0.02363378f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x, -0.06751894f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x, 0.5f);")
			.WriteLine("p *= Single.Sqrt(1f - x);")
			.WriteWhitespace()
			.WriteLine("return negative ? 1f - p : p;")
			.EndBlock();

		return builder.ToString();
	}

	/// <summary>
	///   Generates a fast approximation implementation of the inverse cosine divided by π (AcosPi) function for
	///   double-precision floating-point numbers.
	/// </summary>
	/// <param name="flags">FastMath flags that control NaN handling and other optimizations.</param>
	/// <returns>A string containing the C# code for the fast AcosPi implementation.</returns>
	private static string GenerateFastAcosPiMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast polynomial approximation of inverse cosine divided by π (AcosPi) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Returns Acos(x) / π with higher precision coefficients. Handles negative values and optional NaN checks.</remarks>")
			.WriteLine("""/// <param name="x">Input value in the range [-1, 1].</param>""")
			.WriteLine("/// <returns>Approximate inverse cosine value divided by π, in the range [0, 1].</returns>")
			.WriteLine("private static double FastAcosPi(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("var negative = x < 0.0;")
			.WriteLine("x = Double.Abs(x);")
			.WriteLine("if (x > 1.0) x = 1.0;")
			.WriteWhitespace()
			.WriteLine("var p = Double.FusedMultiplyAdd(-0.0059622704862860465, x, 0.023633778501171472);")
			.WriteLine("p = Double.FusedMultiplyAdd(p, x, -0.067518943563376579);")
			.WriteLine("p = Double.FusedMultiplyAdd(p, x, 0.5);")
			.WriteLine("p *= Double.Sqrt(1.0 - x);")
			.WriteWhitespace()
			.WriteLine("return negative ? 1.0 - p : p;")
			.EndBlock();

		return builder.ToString();
	}
}