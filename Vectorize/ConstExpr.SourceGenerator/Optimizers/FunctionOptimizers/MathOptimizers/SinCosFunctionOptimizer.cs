using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinCosFunctionOptimizer() : BaseMathFunctionOptimizer("SinCos", n => n is 1), IBaseMathCustomImplementation
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
			SpecialType.System_Single => GenerateFastSinCosMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastSinCosMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
	}

	private static string GenerateFastSinCosMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast simultaneous sine and cosine approximation for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses argument reduction and paired polynomial approximations for sine and cosine.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>A tuple containing approximate sine and cosine values.</returns>")
			.WriteLine("private static (float Sin, float Cos) FastSinCos(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return (Single.NaN, Single.NaN);");
		}

		builder.WriteWhitespace()
			.WriteLine("x -= Single.Round(x * 0.15915494309189535f) * Single.Tau;")
			.WriteWhitespace()
			.WriteLine("var xSign = Single.CopySign(1.0f, x);")
			.WriteLine("var absX  = Single.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var over    = absX > 1.5707963267948966f;")
			.WriteLine("var sinArg  = over ? Single.Pi - absX : absX;")
			.WriteLine("var cosSign = over ? -1.0f : 1.0f;")
			.WriteWhitespace()
			.WriteLine("var x2 = sinArg * sinArg;")
			.WriteWhitespace()
			.WriteLine("var sinVal = -0.00019840874f;")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "x2", 0.0083333310f)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "x2", -0.16666667f)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "x2", 1.0f)};")
			.WriteLine("sinVal *= sinArg;")
			.WriteLine("sinVal  = Single.CopySign(sinVal, xSign);")
			.WriteWhitespace()
			.WriteLine("var cosVal = 0.0013888397f;")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "x2", -0.041666418f)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "x2", 0.5f)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "x2", -1.0f)};")
			.WriteLine("cosVal += 1.0f;")
			.WriteLine("cosVal *= cosSign;")
			.WriteWhitespace()
			.WriteLine("return (sinVal, cosVal);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinCosMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast simultaneous sine and cosine approximation for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses argument reduction and paired polynomial approximations for sine and cosine.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>A tuple containing approximate sine and cosine values.</returns>")
			.WriteLine("private static (double Sin, double Cos) FastSinCos(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return (Double.NaN, Double.NaN);");
		}

		builder.WriteWhitespace()
			.WriteLine("x -= Double.Round(x * 0.15915494309189533576888) * Double.Tau;")
			.WriteWhitespace()
			.WriteLine("var xSign = Double.CopySign(1.0, x);")
			.WriteLine("var absX  = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var over    = absX > 1.570796326794896619231;")
			.WriteLine("var sinArg  = over ? Double.Pi - absX : absX;")
			.WriteLine("var cosSign = over ? -1.0 : 1.0;")
			.WriteWhitespace()
			.WriteLine("var x2 = sinArg * sinArg;")
			.WriteWhitespace()
			.WriteLine("var sinVal = 2.7557313707070068e-6;")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "x2", -0.00019841269841201856)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "x2", 0.0083333333333331650)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "x2", -0.16666666666666666)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "x2", 1.0)};")
			.WriteLine("sinVal *= sinArg;")
			.WriteLine("sinVal  = Double.CopySign(sinVal, xSign);")
			.WriteWhitespace()
			.WriteLine("var cosVal = -2.6051615464872668e-5;")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "x2", 0.0013888888888887398)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "x2", -0.041666666666666664)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "x2", 0.5)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "x2", -1.0)};")
			.WriteLine("cosVal += 1.0;")
			.WriteLine("cosVal *= cosSign;")
			.WriteWhitespace()
			.WriteLine("return (sinVal, cosVal);");

		builder.EndBlock();

		return builder.ToString();
	}
}