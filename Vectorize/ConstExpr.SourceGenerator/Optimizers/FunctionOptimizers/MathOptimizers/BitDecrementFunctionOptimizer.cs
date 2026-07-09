using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BitDecrementFunctionOptimizer() : BaseMathFunctionOptimizer("BitDecrement", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// BitDecrement(BitIncrement(x)) -> x (inverse operations)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "BitIncrement" }, ArgumentList.Arguments.Count: 1 } innerInv)
		{
			result = innerInv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastBitDecrementMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastBitDecrementMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
	}

	private static string GenerateFastBitDecrementMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast bit-decrement implementation for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 bit manipulation with NaN and infinity handling controlled by FastMath flags.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The next representable float smaller than x.</returns>")
			.WriteLine("private static float FastBitDecrement(float x)")
			.StartBlock();

		if (flags.HasFlag(FastMathFlags.NoNaN))
		{
			if (!flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Single.IsInfinity(x))")
					.StartBlock()
					.WriteLine("return Single.IsPositiveInfinity(x) ? Single.MaxValue : x;")
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
					.WriteLine("return Single.IsPositiveInfinity(x) ? Single.MaxValue : x;")
					.EndBlock();
			}
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(x);")
			.WriteWhitespace()
			.WriteLine("if (bits == 0) return -Single.Epsilon;")
			.WriteWhitespace()
			.WriteLine("bits -= (bits >> 31) | 1;")
			.WriteLine("return BitConverter.Int32BitsToSingle(bits);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastBitDecrementMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast bit-decrement implementation for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 bit manipulation with NaN and infinity handling controlled by FastMath flags.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The next representable double smaller than x.</returns>")
			.WriteLine("private static double FastBitDecrement(double x)")
			.StartBlock();

		if (flags.HasFlag(FastMathFlags.NoNaN))
		{
			if (!flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Double.IsInfinity(x))")
					.StartBlock()
					.WriteLine("return Double.IsPositiveInfinity(x) ? Double.MaxValue : x;")
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
					.WriteLine("return Double.IsPositiveInfinity(x) ? Double.MaxValue : x;")
					.EndBlock();
			}
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(x);")
			.WriteWhitespace()
			.WriteLine("if (bits == 0L) return -Double.Epsilon;")
			.WriteWhitespace()
			.WriteLine("bits -= (bits >> 63) | 1L;")
			.WriteLine("return BitConverter.Int64BitsToDouble(bits);");

		builder.EndBlock();

		return builder.ToString();
	}
}