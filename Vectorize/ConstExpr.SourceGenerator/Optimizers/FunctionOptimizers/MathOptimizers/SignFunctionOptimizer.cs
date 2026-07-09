using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SignFunctionOptimizer() : BaseMathFunctionOptimizer("Sign", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryGenerateCustomImplementation(context, paramType, out var method))
		{
			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as Sign call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	public bool TryGenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out MethodDeclarationSyntax? result)
	{
		result = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSignMethodFloat(),
			SpecialType.System_Double => GenerateFastSignMethodDouble(),
			_ => null
		});

		if (result is not null)
		{
			context.AdditionalSyntax.TryAdd(result, false);
			return true;
		}

		return false;
	}

	private static string GenerateFastSignMethodFloat()
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast sign implementation for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 sign-bit extraction and returns -1, 0, or 1.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The sign of x as an integer.</returns>")
			.WriteLine("private static int FastSign(float x)")
			.StartBlock()
			.WriteLine("if (x == 0.0f)")
			.StartBlock()
			.WriteLine("return 0;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("return 1 | (bits >> 31);")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSignMethodDouble()
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast sign implementation for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 sign-bit extraction and returns -1, 0, or 1.</remarks>")
			.WriteLine("/// <param name=\"x\">Input floating-point value.</param>")
			.WriteLine("/// <returns>The sign of x as an integer.</returns>")
			.WriteLine("private static int FastSign(double x)")
			.StartBlock()
			.WriteLine("if (x == 0.0)")
			.StartBlock()
			.WriteLine("return 0;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("return 1 | (int)(bits >> 63);")
			.EndBlock();

		return builder.ToString();
	}
}