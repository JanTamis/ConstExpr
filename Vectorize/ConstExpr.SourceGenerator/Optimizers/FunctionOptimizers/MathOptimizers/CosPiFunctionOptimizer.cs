using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CosPiFunctionOptimizer() : BaseMathFunctionOptimizer("CosPi", n => n is 1), IBaseMathCustomImplementation
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
			SpecialType.System_Single => GenerateFastCosPiMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastCosPiMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
	}

	private static string GenerateFastCosPiMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of cosine divided by π (CosPi) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses argument reduction and a polynomial approximation with optional NaN handling. Returns cos(πx).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate cosine value divided by π.</returns>")
			.WriteLine("private static float FastCosPi(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteWhitespace()
			.WriteLine("x -= Single.Round(x * 0.5f) * 2.0f;")
			.WriteLine("x  = Single.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var v  = (x - 0.5f) * Single.Pi;")
			.WriteLine("var v2 = v * v;")
			.WriteLine("var r  = -0.00019841271f;")
			.WriteLine($"r = {multiplyAdd("r", "v2", 0.008333333f)};")
			.WriteLine($"r = {multiplyAdd("r", "v2", -0.16666667f)};")
			.WriteLine($"r = {multiplyAdd("r", "v2", 1.0f)};")
			.WriteLine("return -(v * r);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCosPiMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of cosine divided by π (CosPi) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses argument reduction and a polynomial approximation with optional NaN handling. Returns cos(πx).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate cosine value divided by π.</returns>")
			.WriteLine("private static double FastCosPi(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteWhitespace()
			.WriteLine("x -= Double.Round(x * 0.5) * 2.0;")
			.WriteLine("x  = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var v  = (x - 0.5) * Double.Pi;")
			.WriteLine("var v2 = v * v;")
			.WriteLine("var r  = -2.5052108385441720e-8;")
			.WriteLine($"r = {multiplyAdd("r", "v2", 2.7557319223985888e-6)};")
			.WriteLine($"r = {multiplyAdd("r", "v2", -0.00019841269841269841)};")
			.WriteLine($"r = {multiplyAdd("r", "v2", 0.008333333333333333)};")
			.WriteLine($"r = {multiplyAdd("r", "v2", -0.16666666666666666)};")
			.WriteLine($"r = {multiplyAdd("r", "v2", 1.0)};")
			.WriteLine("return -(v * r);");

		builder.EndBlock();

		return builder.ToString();
	}
}