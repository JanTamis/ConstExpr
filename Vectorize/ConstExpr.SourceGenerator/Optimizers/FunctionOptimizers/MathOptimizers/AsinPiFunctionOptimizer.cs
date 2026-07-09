using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinPiFunctionOptimizer() : BaseMathFunctionOptimizer("AsinPi", n => n is 1), IBaseMathCustomImplementation
{
	/// <summary>
	///   Attempts to optimize a Math.AsinPi function call by generating a fast approximation implementation.
	/// </summary>
	/// <param name="context">The optimizer context containing method arguments and FastMath flags.</param>
	/// <param name="paramType">The type symbol of the parameter (float or double).</param>
	/// <param name="result">The optimized syntax node if successful; otherwise null.</param>
	/// <returns>True if optimization was successful; otherwise false.</returns>
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAsinPiMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastAsinPiMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastAsinPiMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);
		var sqrtInvocation = GetMethodInvocation<SqrtFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of inverse sine divided by π (AsinPi) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a piecewise approximation with FusedMultiplyAdd, returning Asin(x) / π in the range [-0.5, 0.5].</remarks>")
			.WriteLine("/// <param name=\"x\">Input value in the range [-1, 1].</param>")
			.WriteLine("/// <returns>Approximate inverse sine value divided by π.</returns>")
			.WriteLine("private static float FastAsinPi(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;")
				.WriteWhitespace();
		}

		builder.WriteLine("if (x < -1.0f) x = -1.0f;")
			.WriteLine("if (x > 1.0f) x = 1.0f;")
			.WriteWhitespace()
			.WriteLine($"var xa = {absInvocation}<float, uint>(x);")
			.WriteWhitespace()
			.WriteLine("if (xa < 0.5f)")
			.StartBlock()
			.WriteLine("var x2 = xa * xa;")
			.WriteLine("var ret = 0.16666667f;")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0f)};")
			.WriteLine("ret = ret * xa * 0.31830988618379067f;")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(ret, x);")
			.EndBlock()
			.WriteLine("else")
			.StartBlock()
			.WriteLine("var onemx = 1.0f - xa;")
			.WriteLine($"var sqrt_onemx = {sqrtInvocation}(onemx);")
			.WriteWhitespace()
			.WriteLine($"var ret = {multiplyAdd(-0.0187293f, "xa", 0.0742610f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "xa", -0.2121144f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "xa", 1.5707288f)};")
			.WriteLine("ret = ret * sqrt_onemx;")
			.WriteLine($"ret = {multiplyAdd("-ret", 0.31830988618379067f, 0.5f)};")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(ret, x);")
			.EndBlock()
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAsinPiMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);
		var sqrtInvocation = GetMethodInvocation<SqrtFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of inverse sine divided by π (AsinPi) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a piecewise approximation with FusedMultiplyAdd, returning Asin(x) / π in the range [-0.5, 0.5].</remarks>")
			.WriteLine("/// <param name=\"x\">Input value in the range [-1, 1].</param>")
			.WriteLine("/// <returns>Approximate inverse sine value divided by π.</returns>")
			.WriteLine("private static double FastAsinPi(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x < -1.0) x = -1.0;")
			.WriteLine("if (x > 1.0) x = 1.0;")
			.WriteWhitespace()
			.WriteLine($"var xa = {absInvocation}<double, ulong>(x);")
			.WriteWhitespace()
			.WriteLine("if (xa < 0.5)")
			.StartBlock()
			.WriteLine("var x2 = xa * xa;")
			.WriteLine("var ret = 0.16666666666666666;  // 1/6")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0)};")
			.WriteLine("ret = ret * xa * 0.31830988618379067;  // 1/π")
			.WriteLine($"return {copySignInvocation}(ret, x);")
			.EndBlock()
			.WriteLine("else")
			.StartBlock()
			.WriteLine("var onemx = 1.0 - xa;")
			.WriteLine($"var sqrt_onemx = {sqrtInvocation}(onemx);")
			.WriteWhitespace()
			.WriteLine($"var ret = {multiplyAdd(-0.0187293, "xa", 0.0742610)};")
			.WriteLine($"ret = {multiplyAdd("ret", "xa", -0.2121144)};")
			.WriteLine($"ret = {multiplyAdd("ret", "xa", 1.5707288)};")
			.WriteLine("ret = ret * sqrt_onemx;")
			.WriteLine($"ret = {multiplyAdd("-ret", 0.31830988618379067, 0.5)};")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(ret, x);")
			.EndBlock()
			.EndBlock();

		return builder.ToString();
	}
}