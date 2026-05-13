using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinCosPiFunctionOptimizer() : BaseMathFunctionOptimizer("SinCosPi", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSinCosPiMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastSinCosPiMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastSinCosPiMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static (float Sin, float Cos) FastSinCosPi(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return (Single.NaN, Single.NaN);");
		}

		builder
			// .WriteLine("// Fast simultaneous sine(π*x) and cosine(π*x) calculation")
			// .WriteLine("// Uses optimized polynomial approximation with branchless operations")
			.WriteWhitespace()
			// .WriteLine("// Range reduction: bring x to [-1, 1]")
			.WriteLine("x -= Single.Round(x * 0.5f) * 2.0f;")
			.WriteWhitespace()
			// .WriteLine("// Store original sign for sine and work with absolute value")
			.WriteLine("var originalSign = x;")
			.WriteLine("var absX = Single.Abs(x);")
			.WriteWhitespace()
			// .WriteLine("// Determine if we're in upper or lower half [0, 0.5] vs (0.5, 1]")
			// .WriteLine("// For upper half: sin(π*x) = sin(π*(1-x)), cos(π*x) = -cos(π*(1-x))")
			.WriteLine("var inUpperHalf = absX > 0.5f;")
			.WriteLine("var u = inUpperHalf ? (1.0f - absX) : absX;")
			.WriteWhitespace()
			// .WriteLine("// u² shared for both polynomials — π absorbed into coefficients (saves 1 FMUL vs px = u*π)")
			.WriteLine("var u2 = u * u;")
			.WriteWhitespace()
			// .WriteLine("// sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040))))")
			.WriteLine("var sinVal = -0.5992645f;")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, u2,  2.5501640f);")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, u2, -5.1677128f);")
			.WriteLine("sinVal = Single.FusedMultiplyAdd(sinVal, u2,  3.1415927f);")
			.WriteLine("sinVal = sinVal * u;")
			.WriteWhitespace()
			// .WriteLine("// Apply original sign using CopySign (branchless)")
			.WriteLine("sinVal = Single.CopySign(sinVal, originalSign);")
			.WriteWhitespace()
			// .WriteLine("// cospi(u) = 1 + u²·(−π²/2 + u²·(π⁴/24 + u²·(−π⁶/720)))")
			.WriteLine("var cosVal = -1.3352627f;")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, u2,  4.0587121f);")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, u2, -4.9348022f);")
			.WriteLine("cosVal = Single.FusedMultiplyAdd(cosVal, u2,  1.0f);")
			.WriteWhitespace()
			// .WriteLine("// For upper half, negate cosine")
			.WriteLine("cosVal = inUpperHalf ? -cosVal : cosVal;")
			.WriteWhitespace()
			.WriteLine("return (sinVal, cosVal);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinCosPiMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static (double Sin, double Cos) FastSinCosPi(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return (Double.NaN, Double.NaN);");
		}

		builder
			// .WriteLine("// Fast simultaneous sine(π*x) and cosine(π*x) calculation")
			// .WriteLine("// Uses optimized polynomial approximation with branchless operations")
			.WriteWhitespace()
			// .WriteLine("// Range reduction: bring x to [-1, 1]")
			.WriteLine("x -= Double.Round(x * 0.5) * 2.0;")
			.WriteWhitespace()
			// .WriteLine("// Store original sign for sine and work with absolute value")
			.WriteLine("var originalSign = x;")
			.WriteLine("var absX = Double.Abs(x);")
			.WriteWhitespace()
			// .WriteLine("// Determine if we're in upper or lower half [0, 0.5] vs (0.5, 1]")
			// .WriteLine("// For upper half: sin(π*x) = sin(π*(1-x)), cos(π*x) = -cos(π*(1-x))")
			.WriteLine("var inUpperHalf = absX > 0.5;")
			.WriteLine("var u = inUpperHalf ? (1.0 - absX) : absX;")
			.WriteWhitespace()
			// .WriteLine("// u² shared for both polynomials — π absorbed into coefficients (saves 1 FMUL vs px = u*π)")
			.WriteLine("var u2 = u * u;")
			.WriteWhitespace()
			// .WriteLine("// sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040 + u²·(π⁹/362880)))))")
			.WriteLine("var sinVal = 0.08214588661112823;")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, u2, -0.5992645293218801);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, u2,  2.5501640398773455);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, u2, -5.1677127800499706);")
			.WriteLine("sinVal = Double.FusedMultiplyAdd(sinVal, u2,  3.1415926535897932);")
			.WriteLine("sinVal = sinVal * u;")
			.WriteWhitespace()
			// .WriteLine("// Apply original sign using CopySign (branchless)")
			.WriteLine("sinVal = Double.CopySign(sinVal, originalSign);")
			.WriteWhitespace()
			// .WriteLine("// cospi(u) = 1 + u²·(−π²/2 + u²·(π⁴/24 + u²·(−π⁶/720 + u²·(π⁸/40320))))")
			.WriteLine("var cosVal = 0.23533075157732439;")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, u2, -1.3352627312227247);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, u2,  4.0587121264167682);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, u2, -4.9348022005446793);")
			.WriteLine("cosVal = Double.FusedMultiplyAdd(cosVal, u2,  1.0);")
			.WriteWhitespace()
			// .WriteLine("// For upper half, negate cosine")
			.WriteLine("cosVal = inUpperHalf ? -cosVal : cosVal;")
			.WriteWhitespace()
			.WriteLine("return (sinVal, cosVal);");

		builder.EndBlock();

		return builder.ToString();
	}
}