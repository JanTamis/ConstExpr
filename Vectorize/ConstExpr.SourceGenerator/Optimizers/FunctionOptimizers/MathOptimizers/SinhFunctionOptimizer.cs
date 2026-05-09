using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinhFunctionOptimizer() : BaseMathFunctionOptimizer("Sinh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSinhMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastSinhMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastSinhMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastSinh(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var sign = x;")
			.WriteLine("x = Single.Abs(x);")
			.WriteLine("")
			.WriteLine("// exp overflows to +Inf for x > ~88.72; return ±Inf with correct sign immediately")
			.WriteLine("if (x > 88.0f) return Single.CopySign(float.PositiveInfinity, sign);")
			.WriteLine("")
			.WriteLine("var ex = Single.Exp(x);")
			.WriteLine("")
			.WriteLine("// One Newton-Raphson step on ReciprocalEstimate restores ~24-bit precision")
			.WriteLine("// (raw estimate is only ~12-bit accurate, causing ~333× worse error than float epsilon at x=1)")
			.WriteLine("// r' = r * (2 - ex * r)")
			.WriteLine("var r = Single.ReciprocalEstimate(ex);")
			.WriteLine("r *= Single.FusedMultiplyAdd(-ex, r, 2.0f);")
			.WriteLine("")
			.WriteLine("return Single.CopySign((ex - r) * 0.5f, sign);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastSinhMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastSinh(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("var sign = x;")
			.WriteLine("x = Double.Abs(x);")
			.WriteLine("")
			.WriteLine("// exp overflows to +Inf for x > ~709.78; return ±Inf with correct sign immediately")
			.WriteLine("if (x > 709.0) return Double.CopySign(double.PositiveInfinity, sign);")
			.WriteLine("")
			.WriteLine("var ex = Double.Exp(x);")
			.WriteLine("")
			.WriteLine("// Division gives full double precision for 1/ex.")
			.WriteLine("// Double.ReciprocalEstimate is only ~14-bit accurate, causing catastrophic")
			.WriteLine("// precision loss — using FDIV here is both correct and comparable in cost.")
			.WriteLine("return Double.CopySign((ex - 1.0 / ex) * 0.5, sign);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}