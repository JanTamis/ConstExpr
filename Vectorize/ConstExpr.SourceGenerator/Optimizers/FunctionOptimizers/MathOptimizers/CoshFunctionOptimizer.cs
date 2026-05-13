using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CoshFunctionOptimizer() : BaseMathFunctionOptimizer("Cosh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastCoshMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastCoshMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastCoshMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastCosh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("x = Single.Abs(x);")
			.WriteWhitespace()
			// .WriteLine("// exp overflows to +Inf for x > ~88.72; return +Inf immediately")
			.WriteLine("if (x > 88.0f) return float.PositiveInfinity;")
			.WriteWhitespace()
			.WriteLine("var ex = Single.Exp(x);")
			.WriteWhitespace()
			// .WriteLine("// One Newton-Raphson step on ReciprocalEstimate restores ~24-bit precision")
			// .WriteLine("// (raw estimate is only ~12-bit accurate, which causes ~375× worse error than float epsilon)")
			// .WriteLine("// r' = r * (2 - ex * r)")
			.WriteLine("var r = Single.ReciprocalEstimate(ex);")
			.WriteLine("r *= Single.FusedMultiplyAdd(-ex, r, 2.0f);")
			.WriteWhitespace()
			.WriteLine("return (ex + r) * 0.5f;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCoshMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastCosh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("x = Double.Abs(x);")
			.WriteWhitespace()
			// .WriteLine("// exp overflows to +Inf for x > ~709.78; return +Inf immediately")
			.WriteLine("if (x > 709.0) return double.PositiveInfinity;")
			.WriteWhitespace()
			.WriteLine("var ex = Double.Exp(x);")
			.WriteWhitespace()
			// .WriteLine("// Division gives full double precision for 1/ex.")
			// .WriteLine("// Double.ReciprocalEstimate is only ~14-bit accurate, causing catastrophic")
			// .WriteLine("// precision loss — using FDIV here is both correct and comparable in cost.")
			.WriteLine("return (ex + 1.0 / ex) * 0.5;");

		builder.EndBlock();

		return builder.ToString();
	}
}