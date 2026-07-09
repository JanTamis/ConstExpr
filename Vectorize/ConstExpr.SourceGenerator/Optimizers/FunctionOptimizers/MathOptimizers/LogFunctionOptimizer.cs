using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class LogFunctionOptimizer() : BaseMathFunctionOptimizer("Log", n => n is 1 or 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Log(Exp(x)) => x (inverse operation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastLogMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastLogMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			if (context.VisitedParameters.Count == 1)
			{
				// Log(x) => FastLog(x)
				result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
				return true;
			}

			result = DivideExpression(
				CreateInvocation(method.Identifier.Text, context.VisitedParameters[0]),
				CreateInvocation(method.Identifier.Text, context.VisitedParameters[1]));
			return true;
		}

		result = null;
		return false;
	}

	private static string GenerateFastLogMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of the natural logarithm (Log) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses exponent extraction and a polynomial approximation for the mantissa. Returns ln(x).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate natural logarithm of x.</returns>")
			.WriteLine("private static float FastLog(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x) || x < 0f) return Single.NaN;");
		}

		builder.WriteLine("if (x == 0f) return Single.NegativeInfinity;");

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;");
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var e    = (bits >> 23) - 127;")
			.WriteLine("var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);")
			.WriteWhitespace()
			.WriteLine($"var lnm = {multiplyAdd(-0.056570851f, "m", 0.447178975f)};")
			.WriteLine($"lnm     = {multiplyAdd("lnm", "m", -1.469956800f)};")
			.WriteLine($"lnm     = {multiplyAdd("lnm", "m", 2.821202636f)};")
			.WriteLine($"lnm     = {multiplyAdd("lnm", "m", -1.741793927f)};")
			.WriteWhitespace()
			.WriteLine($"return {multiplyAdd("e", 0.6931471805599453f, "lnm")};");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastLogMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of the natural logarithm (Log) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses exponent extraction and a polynomial approximation for the mantissa. Returns ln(x).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate natural logarithm of x.</returns>")
			.WriteLine("private static double FastLog(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x) || x < 0.0) return Double.NaN;");
		}

		builder.WriteLine("if (x == 0.0) return Double.NegativeInfinity;");

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;");
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("var e    = (int)((bits >> 52) - 1023L);")
			.WriteLine("var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);")
			.WriteWhitespace()
			.WriteLine($"var lnm = {multiplyAdd(-0.056570851, "m", 0.447178975)};")
			.WriteLine($"lnm     = {multiplyAdd("lnm", "m", -1.469956800)};")
			.WriteLine($"lnm     = {multiplyAdd("lnm", "m", 2.821202636)};")
			.WriteLine($"lnm     = {multiplyAdd("lnm", "m", -1.741793927)};")
			.WriteWhitespace()
			.WriteLine($"return {multiplyAdd("e", 0.6931471805599453094172321214581766, "lnm")};");

		builder.EndBlock();

		return builder.ToString();
	}
}