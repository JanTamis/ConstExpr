using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CbrtFunctionOptimizer() : BaseMathFunctionOptimizer("Cbrt", n => n is 1), IBaseMathCustomImplementation
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
			SpecialType.System_Single => GenerateFastCbrtMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastCbrtMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
	}

	private static string GenerateFastCbrtMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast cube-root implementation for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses exponent bias approximation with Newton-style refinement and optional NaN handling.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The real cube root of x.</returns>")
			.WriteLine("private static float FastCbrt(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x == 0.0f) return 0.0f;")
			.WriteWhitespace()
			.WriteLine($"var absX = {absInvocation}<float, uint>(x);")
			.WriteWhitespace()
			.WriteLine("var i = BitConverter.SingleToInt32Bits(absX);")
			.WriteLine("i = 0x2a517d47 + i / 3;")
			.WriteLine("var y = BitConverter.Int32BitsToSingle(i);")
			.WriteWhitespace()
			.WriteLine("var y2 = y * y;")
			.WriteLine("var y3 = y2 * y;")
			.WriteLine("var twoA = absX + absX;")
			.WriteLine($"y = y * {multiplyAdd(1.0f, "y3", "twoA")} / {multiplyAdd(2.0f, "y3", "absX")};")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(y, x);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCbrtMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast cube-root implementation for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses exponent bias approximation with Newton-style refinement and optional NaN handling.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The real cube root of x.</returns>")
			.WriteLine("private static double FastCbrt(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x == 0.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine($"var absX = {absInvocation}<double, ulong>(x);")
			.WriteWhitespace()
			.WriteLine("var i = BitConverter.DoubleToInt64Bits(absX);")
			.WriteLine("i = 0x2a9f8b7cef1d0da0L + i / 3;")
			.WriteLine("var y = BitConverter.Int64BitsToDouble(i);")
			.WriteWhitespace()
			.WriteLine("y = (y + y + absX / (y * y)) / 3.0;")
			.WriteWhitespace()
			.WriteLine("var y2 = y * y;")
			.WriteLine("var y3 = y2 * y;")
			.WriteLine("var twoA = absX + absX;")
			.WriteLine($"y = y * {multiplyAdd(1.0, "y3", "twoA")} / {multiplyAdd(2.0, "y3", "absX")};")
			.WriteWhitespace()
			.WriteLine($"return {copySignInvocation}(y, x);");

		builder.EndBlock();

		return builder.ToString();
	}
}