using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinFunctionOptimizer() : BaseMathFunctionOptimizer("Asin", n => n is 1)
{
	/// <summary>
	///   Attempts to optimize a Math.Asin function call by generating a fast approximation implementation.
	/// </summary>
	/// <param name="context">The optimizer context containing method arguments and FastMath flags.</param>
	/// <param name="paramType">The type symbol of the parameter (float or double).</param>
	/// <param name="result">The optimized syntax node if successful; otherwise null.</param>
	/// <returns>True if optimization was successful; otherwise false.</returns>
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAsinMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAsinMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastAsinMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse sine (Asin) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a piecewise polynomial approximation with FusedMultiplyAdd and special handling near zero and near one.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value in the range [-1, 1].</param>")
			.WriteLine("/// <returns>Approximate inverse sine value in radians, in the range [-π/2, π/2].</returns>")
			.WriteLine("private static float FastAsin(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x < -1.0f) x = -1.0f;")
			.WriteLine("if (x > 1.0f)  x =  1.0f;")
			.WriteLine("var xa = Single.Abs(x);")
			.WriteLine("if (xa < 0.5f)")
			.StartBlock()
			.WriteLine("var x2 = xa * xa;")
			.WriteLine("var ret = 0.16666667f;")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);")
			.WriteLine("ret *= xa;")
			.WriteWhitespace()
			.WriteLine("return Single.CopySign(ret, x);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var onemx = 1.0f - xa;")
			.WriteLine("var sqrtOnemx = Single.Sqrt(onemx);")
			.WriteLine("var p = -0.0187293f;")
			.WriteWhitespace()
			.WriteLine("p = Single.FusedMultiplyAdd(p, xa,  0.0742610f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, xa, -0.2121144f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, xa,  1.5707288f);")
			.WriteLine("p *= sqrtOnemx;")
			.WriteLine("p = 1.5707963267948966f - p;")
			.WriteWhitespace()
			.WriteLine("return Single.CopySign(p, x);")
			.RemoveIndent();

		return builder.ToString();
	}

	private static string GenerateFastAsinMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse sine (Asin) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a piecewise polynomial approximation with FusedMultiplyAdd and special handling near zero and near one.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value in the range [-1, 1].</param>")
			.WriteLine("/// <returns>Approximate inverse sine value in radians, in the range [-π/2, π/2].</returns>")
			.WriteLine("private static double FastAsin(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x < -1.0) x = -1.0;")
			.WriteLine("if (x > 1.0)  x =  1.0;")
			.WriteWhitespace()
			.WriteLine("var xa = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("if (xa < 0.5)")
			.StartBlock()
			.WriteLine("var x2 = xa * xa;")
			.WriteLine("var ret = 0.16666666666666666;")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, x2, 1.0);")
			.WriteLine("ret *= xa;")
			.WriteWhitespace()
			.WriteLine("return Double.CopySign(ret, x);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var onemx = 1.0 - xa;")
			.WriteLine("var sqrtOnemx = Double.Sqrt(onemx);")
			.WriteLine("var p = -0.0187293;")
			.WriteWhitespace()
			.WriteLine("p = Double.FusedMultiplyAdd(p, xa,  0.0742610);")
			.WriteLine("p = Double.FusedMultiplyAdd(p, xa, -0.2121144);")
			.WriteLine("p = Double.FusedMultiplyAdd(p, xa,  1.5707288);")
			.WriteLine("p *= sqrtOnemx;")
			.WriteLine("p = 1.5707963267948966 - p;")
			.WriteWhitespace()
			.WriteLine("return Double.CopySign(p, x);")
			.EndBlock();

		return builder.ToString();
	}
}