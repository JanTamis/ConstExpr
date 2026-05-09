using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CosFunctionOptimizer() : BaseMathFunctionOptimizer("Cos", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastCosMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastCosMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastCosMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastCos(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("// Fast cosine approximation using minimax polynomial")
			.WriteLine("// Branchless range reduction to [-π, π]:")
			.WriteLine("// Round(x/τ) compiles to a single FRINTN (ARM64) / ROUNDSS (x64) —")
			.WriteLine("// avoids FDIV and conditional branches of the Floor-based approach.")
			.WriteLine("x -= Single.Round(x * (1f / Single.Tau)) * Single.Tau;")
			.WriteLine("")
			.WriteLine("// Use symmetry: cos(-x) = cos(x): fold to [0, π]")
			.WriteLine("x = Single.Abs(x);")
			.WriteLine("")
			.WriteLine("// Degree-8 minimax polynomial for cos(x) on [0, π], evaluated in x² (4 FMA)")
			.WriteLine("var x2 = x * x;")
			.WriteLine("var ret = 0.0003538394f;                                        // x^8 term")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, x2, -0.0041666418f);        // x^6 term")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, x2,  0.041666666f);         // x^4 term")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, x2, -0.5f);                 // x^2 term")
			.WriteLine("ret = Single.FusedMultiplyAdd(ret, x2,  1.0f);                 // constant term")
			.WriteLine("")
			.WriteLine("return ret;");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastCosMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastCos(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("// Fast cosine approximation using minimax polynomial")
			.WriteLine("// Branchless range reduction to [-π, π]:")
			.WriteLine("// Round(x/τ) compiles to a single FRINTA (ARM64) / ROUNDSD (x64) —")
			.WriteLine("// avoids FDIV and conditional branches of the Floor-based approach.")
			.WriteLine("x -= Double.Round(x * (1.0 / Double.Tau)) * Double.Tau;")
			.WriteLine("")
			.WriteLine("// Use symmetry: cos(-x) = cos(x): fold to [0, π]")
			.WriteLine("x = Double.Abs(x);")
			.WriteLine("")
			.WriteLine("// Degree-10 minimax polynomial for cos(x) on [0, π], evaluated in x² (5 FMA)")
			.WriteLine("var x2 = x * x;")
			.WriteLine("var ret = -1.1940250944959890e-7;                                         // x^10 term")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, x2,  2.0876755527587203e-5);           // x^8 term")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, x2, -0.0013888888888739916);           // x^6 term")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, x2,  0.041666666666666602);            // x^4 term")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, x2, -0.5);                             // x^2 term")
			.WriteLine("ret = Double.FusedMultiplyAdd(ret, x2,  1.0);                             // constant term")
			.WriteLine("")
			.WriteLine("return ret;");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}