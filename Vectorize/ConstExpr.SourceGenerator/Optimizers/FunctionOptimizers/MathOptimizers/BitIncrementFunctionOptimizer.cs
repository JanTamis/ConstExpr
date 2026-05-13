using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BitIncrementFunctionOptimizer() : BaseMathFunctionOptimizer("BitIncrement", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// BitIncrement(BitDecrement(x)) -> x (inverse operations)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "BitDecrement" }, ArgumentList.Arguments.Count: 1 } innerInv)
		{
			result = innerInv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastBitIncrementMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastBitIncrementMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as BitIncrement call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastBitIncrementMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast bit-increment implementation for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 bit manipulation with NaN and infinity handling controlled by FastMath flags.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The next representable float greater than x.</returns>")
			.WriteLine("private static float FastBitIncrement(float x)")
			.StartBlock();

		if (flags.HasFlag(FastMathFlags.NoNaN))
		{
			if (!flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Single.IsInfinity(x))")
					.StartBlock()
					.WriteLine("return Single.IsNegativeInfinity(x) ? -Single.MaxValue : x;")
					.EndBlock();
			}
		}
		else
		{
			if (flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Single.IsNaN(x)) return x;");
			}
			else
			{
				builder.WriteLine("if (!Single.IsFinite(x))")
					.StartBlock()
					.WriteLine("return Single.IsNegativeInfinity(x) ? -Single.MaxValue : x;")
					.EndBlock();
			}
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = System.BitConverter.SingleToInt32Bits(x);")
			.WriteWhitespace()
			.WriteLine("if ((bits & int.MaxValue) == 0) return Single.Epsilon;")
			.WriteWhitespace()
			.WriteLine("bits += (bits >> 31) | 1;")
			.WriteLine("return System.BitConverter.Int32BitsToSingle(bits);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastBitIncrementMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast bit-increment implementation for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 bit manipulation with NaN and infinity handling controlled by FastMath flags.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The next representable double greater than x.</returns>")
			.WriteLine("private static double FastBitIncrement(double x)")
			.StartBlock();

		if (flags.HasFlag(FastMathFlags.NoNaN))
		{
			if (!flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Double.IsInfinity(x))")
					.StartBlock()
					.WriteLine("return Double.IsNegativeInfinity(x) ? -Double.MaxValue : x;")
					.EndBlock();
			}
		}
		else
		{
			if (flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Double.IsNaN(x)) return x;");
			}
			else
			{
				builder.WriteLine("if (!Double.IsFinite(x))")
					.StartBlock()
					.WriteLine("return Double.IsNegativeInfinity(x) ? -Double.MaxValue : x;")
					.EndBlock();
			}
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = System.BitConverter.DoubleToInt64Bits(x);")
			.WriteWhitespace()
			.WriteLine("if ((bits & long.MaxValue) == 0L) return Double.Epsilon;")
			.WriteWhitespace()
			.WriteLine("bits += (bits >> 63) | 1L;")
			.WriteLine("return System.BitConverter.Int64BitsToDouble(bits);");

		builder.EndBlock();

		return builder.ToString();
	}
}