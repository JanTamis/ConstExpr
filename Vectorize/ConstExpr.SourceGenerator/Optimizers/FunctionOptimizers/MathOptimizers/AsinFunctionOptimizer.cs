using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinFunctionOptimizer() : BaseMathFunctionOptimizer("Asin", n => n is 1)
{
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

		builder.WriteLine("private static float FastAsin(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x < -1.0f) x = -1.0f;")
			.WriteLine("if (x > 1.0f)  x =  1.0f;")
			.WriteLine("var xa = Single.Abs(x);")
			.WriteLine("if (xa < 0.5f)")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine("// Taylor: asin(x) ≈ x + x³/6  (max error ~5e-4 near x = 0.5)")
			.WriteLine("var x2 = xa * xa;")
			.WriteLine("var ret = 0.16666667f; // 1/6")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);")
			.WriteLine("ret *= xa;")
			.WriteLine("return Single.CopySign(ret, x);")
			.RemoveIndent()
			.WriteLine("}")
			.WriteLine("")
			.WriteLine("// A&S §4.4.45 minimax polynomial: acos(|x|) = sqrt(1-|x|) * poly(|x|)")
			.WriteLine("// then asin(x) = sign(x) * (π/2 - acos(|x|))")
			.WriteLine("var onemx = 1.0f - xa;")
			.WriteLine("var sqrtOnemx = Single.Sqrt(onemx);")
			.WriteLine("var p = -0.0187293f;")
			.WriteLine("p = Single.FusedMultiplyAdd(p, xa,  0.0742610f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, xa, -0.2121144f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, xa,  1.5707288f);")
			.WriteLine("p *= sqrtOnemx;")
			.WriteLine("p = 1.5707963267948966f - p;")
			.WriteLine("return Single.CopySign(p, x);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastAsinMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastAsin(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x < -1.0) x = -1.0;")
			.WriteLine("if (x > 1.0)  x =  1.0;")
			.WriteLine("var xa = Double.Abs(x);")
			.WriteLine("if (xa < 0.5)")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine("// Taylor: asin(x) ≈ x + x³/6  (max error ~2.8e-3 near x = 0.5)")
			.WriteLine("var x2 = xa * xa;")
			.WriteLine("var ret = 0.16666666666666666; // 1/6")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, x2, 1.0);")
			.WriteLine("ret *= xa;")
			.WriteLine("return Double.CopySign(ret, x);")
			.RemoveIndent()
			.WriteLine("}")
			.WriteLine("")
			.WriteLine("// A&S §4.4.45 minimax polynomial: acos(|x|) = sqrt(1-|x|) * poly(|x|)")
			.WriteLine("// then asin(x) = sign(x) * (π/2 - acos(|x|))")
			.WriteLine("var onemx = 1.0 - xa;")
			.WriteLine("var sqrtOnemx = Double.Sqrt(onemx);")
			.WriteLine("var p = -0.0187293;")
			.WriteLine("p = Double.FusedMultiplyAdd(p, xa,  0.0742610);")
			.WriteLine("p = Double.FusedMultiplyAdd(p, xa, -0.2121144);")
			.WriteLine("p = Double.FusedMultiplyAdd(p, xa,  1.5707288);")
			.WriteLine("p *= sqrtOnemx;")
			.WriteLine("p = 1.5707963267948966 - p;")
			.WriteLine("return Double.CopySign(p, x);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}