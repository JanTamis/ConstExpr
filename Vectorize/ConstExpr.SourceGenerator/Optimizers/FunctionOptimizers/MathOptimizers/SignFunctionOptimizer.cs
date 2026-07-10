using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SignFunctionOptimizer() : BaseMathFunctionOptimizer("Sign", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		if (paramType.IsFloatingNumeric())
		{
			context.AdditionalSyntax.TryAdd(ParseMethodFromString(GenerateFastSignMethod()), false);
		}

		return paramType.SpecialType switch
		{
			SpecialType.System_Single => "FastSign<float, int>",
			SpecialType.System_Double => "FastSign<double, long>",
			_ => base.GenerateCustomImplementation(context, paramType)
		};
	}

	private static string GenerateFastSignMethod()
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast sign implementation for generic floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 sign-bit extraction and returns -1, 0, or 1.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The sign of x as an integer.</returns>")
			.WriteLine("private static int FastSign<T, TBits>(T x) where T : IBinaryFloatingPoint<T> where TBits : IBinaryInteger<TBits>")
			.StartBlock()
			.WriteLine("if (T.IsZero(x))")
			.StartBlock()
			.WriteLine("return 0;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var bits = Unsafe.BitCast<TBits>(x);")
			.WriteLine("return 1 | Int32.CreateChecked((bits >> (Unsafe.SizeOf<TBits>() * 8 - 1)));")
			.EndBlock();

		return builder.ToString();
	}
}