using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class ExpFunctionOptimizer() : BaseMathFunctionOptimizer("Exp", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Exp(Log(x)) => x (inverse operation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Log" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastExpMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastExpMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as Exp call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastExpMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastExp(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x >= 88.0f) return Single.PositiveInfinity;")
			.WriteLine("if (x <= -87.0f) return 0.0f;")
			.WriteLine("")
			.WriteLine("const float INV_LN2 = 1.4426950408889634f;  // log₂(e)")
			.WriteLine("")
			.WriteLine("var kf = x * INV_LN2;")
			.WriteLine("var k  = (int)Single.Round(kf);  // branchless FRINTN + FCVTZS on ARM64")
			.WriteLine("var r  = kf - k;                // fractional bits of log₂(eˣ), r ∈ [-0.5, 0.5]")
			.WriteLine("")
			.WriteLine("// Degree-3 Horner for 2^r: cₙ = ln(2)ⁿ / n!")
			.WriteLine("// Eliminates the FMA(-k, LN2, x) range-reduction step vs Taylor e^r approach.")
			.WriteLine("const float c3 = 0.055504108664821580f;  // ln(2)³ / 6")
			.WriteLine("const float c2 = 0.240226506959100690f;  // ln(2)² / 2")
			.WriteLine("const float c1 = 0.693147180559945309f;  // ln(2)")
			.WriteLine("")
			.WriteLine("var p    = Single.FusedMultiplyAdd(c3, r, c2);")
			.WriteLine("p        = Single.FusedMultiplyAdd(p,  r, c1);")
			.WriteLine("var expR = Single.FusedMultiplyAdd(p,  r, 1.0f);")
			.WriteLine("")
			.WriteLine("return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastExpMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastExp(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x >= 709.0) return Double.PositiveInfinity;")
			.WriteLine("if (x <= -708.0) return 0.0;")
			.WriteLine("")
			.WriteLine("const double INV_LN2 = 1.4426950408889634073599246810018921;  // log₂(e)")
			.WriteLine("")
			.WriteLine("var kf = x * INV_LN2;")
			.WriteLine("var k  = (long)Double.Round(kf);  // branchless on ARM64")
			.WriteLine("var r  = kf - k;                // fractional bits of log₂(eˣ), r ∈ [-0.5, 0.5]")
			.WriteLine("")
			.WriteLine("// Degree-4 Horner for 2^r: cₙ = ln(2)ⁿ / n!")
			.WriteLine("// Eliminates the FMA(-k, LN2, x) range-reduction step vs Taylor e^r approach.")
			.WriteLine("const double c4 = 9.618129107628477232e-3;  // ln(2)⁴ / 24")
			.WriteLine("const double c3 = 5.550410866482157995e-2;  // ln(2)³ / 6")
			.WriteLine("const double c2 = 2.402265069591006909e-1;  // ln(2)² / 2")
			.WriteLine("const double c1 = 6.931471805599453094e-1;  // ln(2)")
			.WriteLine("")
			.WriteLine("var p    = Double.FusedMultiplyAdd(c4, r, c3);")
			.WriteLine("p        = Double.FusedMultiplyAdd(p,  r, c2);")
			.WriteLine("p        = Double.FusedMultiplyAdd(p,  r, c1);")
			.WriteLine("var expR = Double.FusedMultiplyAdd(p,  r, 1.0);")
			.WriteLine("")
			.WriteLine("var bits = (ulong)((k + 1023L) << 52);")
			.WriteLine("return BitConverter.UInt64BitsToDouble(bits) * expR;");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}