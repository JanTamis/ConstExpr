using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinPiFunctionOptimizer() : BaseMathFunctionOptimizer("AsinPi", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAsinPiMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAsinPiMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastAsinPiMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastAsinPi(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x < -1.0f) x = -1.0f;")
			.WriteLine("if (x > 1.0f) x = 1.0f;")
			.WriteLine("")
			.WriteLine("var xa = Single.Abs(x);")
			.WriteLine("")
			.WriteLine("if (xa < 0.5f)")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine("// Taylor series: asinPi(x) ≈ x/π + x³/(6π)  — avoids sqrt entirely")
			.WriteLine("var x2 = xa * xa;")
			.WriteLine("var ret = 0.16666667f;  // 1/6")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);")
			.WriteLine("ret = ret * xa * 0.31830988618379067f;  // 1/π")
			.WriteLine("return Single.CopySign(ret, x);")
			.RemoveIndent()
			.WriteLine("}")
			.WriteLine("else")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine("// A&S §4.4.45 minimax polynomial: asinPi(x) = 0.5 − sqrt(1−|x|)·poly(|x|)/π")
			.WriteLine("var onemx = 1.0f - xa;")
			.WriteLine("var sqrt_onemx = Single.Sqrt(onemx);")
			.WriteLine("")
			.WriteLine("var ret = -0.0187293f;")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, xa, 0.0742610f);")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, xa, -0.2121144f);")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, xa, 1.5707288f);")
			.WriteLine("ret = ret * sqrt_onemx;")
			.WriteLine("")
			.WriteLine("ret = Single.FusedMultiplyAdd(-ret, 0.31830988618379067f, 0.5f);")
			.WriteLine("return Single.CopySign(ret, x);")
			.RemoveIndent()
			.WriteLine("}");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastAsinPiMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastAsinPi(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x < -1.0) x = -1.0;")
			.WriteLine("if (x > 1.0) x = 1.0;")
			.WriteLine("")
			.WriteLine("var xa = Double.Abs(x);")
			.WriteLine("")
			.WriteLine("if (xa < 0.5)")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine("// Taylor series: asinPi(x) ≈ x/π + x³/(6π)  — avoids sqrt entirely")
			.WriteLine("var x2 = xa * xa;")
			.WriteLine("var ret = 0.16666666666666666;  // 1/6")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, x2, 1.0);")
			.WriteLine("ret = ret * xa * 0.31830988618379067;  // 1/π")
			.WriteLine("return Double.CopySign(ret, x);")
			.RemoveIndent()
			.WriteLine("}")
			.WriteLine("else")
			.WriteLine("{")
			.AddIndent("\t")
			.WriteLine("// A&S §4.4.45 minimax polynomial: asinPi(x) = 0.5 − sqrt(1−|x|)·poly(|x|)/π")
			.WriteLine("var onemx = 1.0 - xa;")
			.WriteLine("var sqrt_onemx = Double.Sqrt(onemx);")
			.WriteLine("")
			.WriteLine("var ret = -0.0187293; ")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, xa, 0.0742610);")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, xa, -0.2121144);")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, xa, 1.5707288);")
			.WriteLine("ret = ret * sqrt_onemx;")
			.WriteLine("")
			.WriteLine("ret = Double.FusedMultiplyAdd(-ret, 0.31830988618379067, 0.5);")
			.WriteLine("return Double.CopySign(ret, x);")
			.RemoveIndent()
			.WriteLine("}");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}