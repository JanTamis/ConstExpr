using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AcosFunctionOptimizer() : BaseMathFunctionOptimizer("Acos", n => n is 1)
{
	/// <summary>
	///   Attempts to optimize a Math.Acos function call by generating a fast approximation implementation.
	/// </summary>
	/// <param name="context">The optimizer context containing method arguments and FastMath flags.</param>
	/// <param name="paramType">The type symbol of the parameter (float or double).</param>
	/// <param name="result">The optimized syntax node if successful; otherwise null.</param>
	/// <returns>True if optimization was successful; otherwise false.</returns>
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAcosMethodFloat(context),
			SpecialType.System_Double => GenerateFastAcosMethodDouble(context),
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

	/// <summary>
	///   Generates a fast approximation implementation of the inverse cosine (Acos) function for single-precision
	///   floating-point numbers.
	///   Uses a polynomial approximation with FusedMultiplyAdd for improved performance.
	/// </summary>
	/// <param name="context">The optimizer context containing method arguments and FastMath flags.</param>
	/// <returns>A string containing the C# code for the fast Acos implementation.</returns>
	private static string GenerateFastAcosMethodFloat(FunctionOptimizerContext context)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast polynomial approximation of inverse cosine (Acos) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses FusedMultiplyAdd for improved performance. Handles negative values and optional NaN checks.</remarks>")
			.WriteLine("""/// <param name="x">Input value in the range [-1, 1].</param>""")
			.WriteLine("/// <returns>Approximate inverse cosine value in radians, in the range [0, π].</returns>")
			.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
			.WriteLine("public static float FastAcos(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var negative = x < 0f;")
			.WriteLine("x = Single.Abs(x);");

		builder.WriteLine("var p = Single.FusedMultiplyAdd(-0.0187293f, x, 0.0742610f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x, -0.2121144f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x, 1.5707288f);")
			.WriteLine("p *= Single.Sqrt(1f - x);")
			.WriteLine("return negative ? Single.Pi - p : p;")
			.EndBlock();

		return builder.ToString();
	}

	/// <summary>
	///   Generates a fast approximation implementation of the inverse cosine (Acos) function for double-precision
	///   floating-point numbers.
	///   Uses a higher-precision polynomial approximation with separate handling for values greater than 0.5.
	/// </summary>
	/// <param name="context">The optimizer context containing method arguments and FastMath flags.</param>
	/// <returns>A string containing the C# code for the fast Acos implementation.</returns>
	private static string GenerateFastAcosMethodDouble(FunctionOptimizerContext context)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast polynomial approximation of inverse cosine (Acos) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a higher-precision polynomial with separate handling for values greater than 0.5. Handles negative values and optional NaN checks.</remarks>")
			.WriteLine("""/// <param name="x">Input value in the range [-1, 1].</param>""")
			.WriteLine("/// <returns>Approximate inverse cosine value in radians, in the range [0, π].</returns>")
			.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
			.WriteLine("public static double FastAcos(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("var negative = x < 0.0;")
			.WriteLine("x = Double.Abs(x);")
			.WriteLine("var big = x > 0.5;")
			.WriteWhitespace()
			.WriteLine("var t = big ? Double.Sqrt((1.0 - x) * 0.5) : x;")
			.WriteLine("var u = t * t;")
			.WriteWhitespace()
			.WriteLine("var p = Double.FusedMultiplyAdd(u, 945.0 / 42240.0, 105.0 / 3456.0);")
			.WriteLine("p = Double.FusedMultiplyAdd(u, p, 15.0 / 336.0);")
			.WriteLine("p = Double.FusedMultiplyAdd(u, p, 3.0 / 40.0);")
			.WriteLine("p = Double.FusedMultiplyAdd(u, p, 1.0 / 6.0);")
			.WriteLine("p = Double.FusedMultiplyAdd(u, p, 1.0);")
			.WriteWhitespace()
			.WriteLine("var asinT = t * p;")
			.WriteLine("var result = big ? 2.0 * asinT : Math.PI / 2.0 - asinT;")
			.WriteWhitespace()
			.WriteLine("return negative ? Math.PI - result : result;")
			.EndBlock();

		return builder.ToString();
	}
}