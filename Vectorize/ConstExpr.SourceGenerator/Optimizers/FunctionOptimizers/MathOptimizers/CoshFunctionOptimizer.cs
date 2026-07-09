using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CoshFunctionOptimizer() : BaseMathFunctionOptimizer("Cosh", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastCoshMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastCoshMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastCoshMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var reciprocalEstimateInvocation = GetMethodInvocation<ReciprocalEstimateFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of hyperbolic cosine (Cosh) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses absolute-value reduction, inline fast-exp base-2 reduction, and optional NaN handling. ~1.1× faster than Single.Exp.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate hyperbolic cosine value.</returns>")
			.WriteLine("private static float FastCosh(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine($"x = {absInvocation}<float, uint>(x);")
			.WriteWhitespace()
			.WriteLine("if (x > 88.0f) return Single.PositiveInfinity;")
			.WriteWhitespace()
			.WriteLine("var kf = x * 1.4426950408889634f;")
			.WriteLine($"var k  = (int){roundInvocation}(kf);")
			.WriteLine("var rf = kf - k;")
			.WriteLine($"var p  = {multiplyAdd(0.055504108664821580f, "rf", 0.240226506959100690f)};")
			.WriteLine($"p      = {multiplyAdd("p", "rf", 0.693147180559945309f)};")
			.WriteLine($"var ex = {multiplyAdd("p", "rf", 1.0f)} * BitConverter.Int32BitsToSingle((k + 127) << 23);")
			.WriteWhitespace()
			.WriteLine($"var r = {reciprocalEstimateInvocation}(ex);")
			.WriteLine($"r *= {multiplyAdd("-ex", "r", 2.0f)};")
			.WriteWhitespace()
			.WriteLine("return (ex + r) * 0.5f;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCoshMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var reciprocalEstimateInvocation = GetMethodInvocation<ReciprocalEstimateFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of hyperbolic cosine (Cosh) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses absolute-value reduction, inline fast-exp base-2 reduction, and optional NaN handling. ~1.6× faster than Double.Exp.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate hyperbolic cosine value.</returns>")
			.WriteLine("private static double FastCosh(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine($"x = {absInvocation}<double, ulong>(x);")
			.WriteWhitespace()
			.WriteLine("if (x > 709.0) return Double.PositiveInfinity;")
			.WriteWhitespace()
			.WriteLine("var kf = x * 1.4426950408889634073599246810018921;")
			.WriteLine($"var k  = (long){roundInvocation}(kf);")
			.WriteLine("var rd = kf - k;")
			.WriteLine($"var p  = {multiplyAdd(9.618129107628477232e-3, "rd", 5.550410866482157995e-2)};")
			.WriteLine($"p      = {multiplyAdd("p", "rd", 2.402265069591006909e-1)};")
			.WriteLine($"p      = {multiplyAdd("p", "rd", 6.931471805599453094e-1)};")
			.WriteLine($"var ex = {multiplyAdd("p", "rd", 1.0)} * BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52));")
			.WriteWhitespace()
			.WriteLine($"return (ex + {reciprocalEstimateInvocation}(ex)) * 0.5;");

		builder.EndBlock();

		return builder.ToString();
	}
}