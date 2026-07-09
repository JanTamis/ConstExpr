using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class ExpFunctionOptimizer() : BaseMathFunctionOptimizer("Exp", n => n is 1), IBaseMathCustomImplementation
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

		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastExpMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastExpMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
	}

	private static string GenerateFastExpMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var roundMethod = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of the natural exponential (Exp) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a base-2 reduction via log₂(e) and a polynomial approximation. Clamps at ±overflow bounds.</remarks>")
			.WriteLine("/// <param name=\"x\">Input exponent value.</param>")
			.WriteLine("/// <returns>Approximate value of eˣ.</returns>")
			.WriteLine("private static float FastExp(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;")
				.WriteLine("if (Single.IsNegativeInfinity(x)) return 0.0f;");
		}

		builder.WriteLine("if (x >= 88.0f) return Single.PositiveInfinity;")
			.WriteLine("if (x <= -87.0f) return 0.0f;")
			.WriteWhitespace()
			.WriteLine("var kf = x * 1.4426950408889634f;")
			.WriteLine($"var k  = (int){roundMethod}(kf);")
			.WriteLine("var r  = kf - k;")
			.WriteWhitespace()
			.WriteLine($"var p    = {multiplyAdd(0.055504108664821580f, "r", 0.240226506959100690f)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 0.693147180559945309f)};")
			.WriteLine($"var expR = {multiplyAdd("p", "r", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("return BitConverter.Int32BitsToSingle((k + 127) << 23) * expR;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastExpMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var roundMethod = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of the natural exponential (Exp) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a base-2 reduction via log₂(e) and a polynomial approximation. Clamps at ±overflow bounds.</remarks>")
			.WriteLine("/// <param name=\"x\">Input exponent value.</param>")
			.WriteLine("/// <returns>Approximate value of eˣ.</returns>")
			.WriteLine("private static double FastExp(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;")
				.WriteLine("if (Double.IsNegativeInfinity(x)) return 0.0;");
		}

		builder.WriteLine("if (x >= 709.0) return Double.PositiveInfinity;")
			.WriteLine("if (x <= -708.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine("var kf = x * 1.4426950408889634073599246810018921;")
			.WriteLine($"var k  = (long){roundMethod}(kf);")
			.WriteLine("var r  = kf - k;")
			.WriteWhitespace()
			.WriteLine($"var p    = {multiplyAdd(9.618129107628477232e-3, "r", 5.550410866482157995e-2)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 2.402265069591006909e-1)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 6.931471805599453094e-1)};")
			.WriteLine($"var expR = {multiplyAdd("p", "r", 1.0)};")
			.WriteWhitespace()
			.WriteLine("var bits = (ulong)((k + 1023L) << 52);")
			.WriteLine("return BitConverter.UInt64BitsToDouble(bits) * expR;");

		builder.EndBlock();

		return builder.ToString();
	}
}