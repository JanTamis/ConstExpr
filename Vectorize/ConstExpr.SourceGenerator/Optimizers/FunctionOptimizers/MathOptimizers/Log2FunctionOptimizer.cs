using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Log2FunctionOptimizer() : BaseMathFunctionOptimizer("Log2", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Log2(Exp2(x)) => x  (inverse-operation cancellation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp2" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastLog2MethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastLog2MethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: delegate to the numeric-helper type's Log2.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastLog2MethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of base-2 logarithm (Log2) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses exponent extraction and a polynomial approximation for the mantissa. Returns log₂(x).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate base-2 logarithm of x.</returns>")
			.WriteLine("private static float FastLog2(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x) || x < 0f) return Single.NaN;");
		}

		builder.WriteLine("if (x == 0f) return Single.NegativeInfinity;");

		if (!flags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;");
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var e    = (bits >> 23) - 127;")
			.WriteLine("var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);")
			.WriteWhitespace()
			.WriteLine("var log2m = Single.FusedMultiplyAdd(-0.081614484f, m, 0.645142871f);")
			.WriteLine("log2m     = Single.FusedMultiplyAdd(log2m, m, -2.120699326f);")
			.WriteLine("log2m     = Single.FusedMultiplyAdd(log2m, m, 4.070134936f);")
			.WriteLine("log2m     = Single.FusedMultiplyAdd(log2m, m, -2.512877389f);")
			.WriteWhitespace()
			.WriteLine("return e + log2m;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastLog2MethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of base-2 logarithm (Log2) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses exponent extraction and a polynomial approximation for the mantissa. Returns log₂(x).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate base-2 logarithm of x.</returns>")
			.WriteLine("private static double FastLog2(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x) || x < 0.0) return Double.NaN;");
		}

		builder.WriteLine("if (x == 0.0) return Double.NegativeInfinity;");

		if (!flags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;");
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("var e    = (int)((bits >> 52) - 1023L);")
			.WriteLine("var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);")
			.WriteWhitespace()
			.WriteLine("var log2m = Double.FusedMultiplyAdd(-0.081614484028, m, 0.645142871432);")
			.WriteLine("log2m     = Double.FusedMultiplyAdd(log2m, m, -2.120699326246);")
			.WriteLine("log2m     = Double.FusedMultiplyAdd(log2m, m, 4.070134936011);")
			.WriteLine("log2m     = Double.FusedMultiplyAdd(log2m, m, -2.512877388986);")
			.WriteWhitespace()
			.WriteLine("return e + log2m;");

		builder.EndBlock();

		return builder.ToString();
	}
}