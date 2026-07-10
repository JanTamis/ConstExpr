using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinFunctionOptimizer() : BaseMathFunctionOptimizer("Sin", n => n is 1), IBaseMathCustomImplementation
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
			SpecialType.System_Single => GenerateFastSinMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastSinMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastSinMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var minInvocation = GetMethodInvocation<MinFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static float FastSin(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteWhitespace()
			.WriteLine("var originalX = x;")
			.WriteWhitespace()
			.WriteLine($"x -= {roundInvocation}(x * (1.0f / Single.Tau)) * Single.Tau;")
			.WriteWhitespace()
			.WriteLine($"x = {absInvocation}(x);")
			.WriteLine($"x = {minInvocation}(x, Single.Pi - x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine($"var ret = {multiplyAdd(-1.9841269841e-4f, "x2", 8.3333333333e-3f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -1.6666666667e-1f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0f)};")
			.WriteLine("ret *= x;")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(ret, originalX);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("private static double FastSin(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteWhitespace()
			.WriteLine("var originalX = x;")
			.WriteWhitespace()
			.WriteLine($"x -= {roundInvocation}(x * (1.0 / Double.Tau)) * Double.Tau;")
			.WriteWhitespace()
			.WriteLine($"x = {absInvocation}(x);")
			.WriteWhitespace()
			.WriteLine("if (x > Double.Pi / 2.0)")
			.StartBlock()
			.WriteLine("x = Double.Pi - x;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine($"var ret = {multiplyAdd(2.6019406621361745e-9, "x2", -1.9839531932589676e-7)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 8.3333333333216515e-6)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.00019841269836761127)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 0.0083333333333332177)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.16666666666666666)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0)};")
			.WriteLine("ret *= x;")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(ret, originalX);");

		builder.EndBlock();

		return builder.ToString();
	}
}