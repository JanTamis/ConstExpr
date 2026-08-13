using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

/// <summary>
///   Optimizer for Math.Cos / MathF.Cos. See SinFunctionOptimizer for the Cody-Waite two-term
///   Tau-reduction rationale (large-|x| precision fix, 5e6 fallback threshold mirroring
///   VectorMath.CosDouble's ARG_HUGE constant).
/// </summary>
public class CosFunctionOptimizer() : BaseMathFunctionOptimizer("Cos", n => n is 1)
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
			SpecialType.System_Single => GenerateFastCosMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastCosMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastCosMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static float FastCos(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (Single.Abs(x) >= 5e6f) return Single.Cos(x);")
			.WriteWhitespace()
			.WriteLine("var xd = (double)x;")
			.WriteLine("var k = Math.Round(xd * (1.0 / Double.Tau));")
			.WriteLine("xd -= k * 6.2831853069365025;")
			.WriteLine("xd -= k * 2.4308402026024769e-10;")
			.WriteLine("x = (float)xd;")
			.WriteWhitespace()
			.WriteLine($"x = {absInvocation}(x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine($"var ret = {multiplyAdd(-2.3344791e-7f, "x2", 2.4512721e-5f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.0013882476f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 0.041666666f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.5f)};")
			.WriteWhitespace()
			.WriteLine($"return {multiplyAdd("ret", "x2", 1.0f)};");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCosMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static double FastCos(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (Double.Abs(x) >= 5e6) return Double.Cos(x);")
			.WriteWhitespace()
			.WriteLine($"var k = {roundInvocation}(x * (1.0 / Double.Tau));")
			.WriteLine("x -= k * 6.2831853069365025;")
			.WriteLine("x -= k * 2.4308402026024769e-10;")
			.WriteWhitespace()
			.WriteLine($"x = {absInvocation}(x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine($"var ret = {multiplyAdd(-1.1940250944959890e-7, "x2", 2.0876755527587203e-5)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.0013888888888739916)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 0.041666666666666602)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.5)};")
			.WriteWhitespace()
			.WriteLine($"return {multiplyAdd("ret", "x2", 1.0)};");

		builder.EndBlock();

		return builder.ToString();
	}
}