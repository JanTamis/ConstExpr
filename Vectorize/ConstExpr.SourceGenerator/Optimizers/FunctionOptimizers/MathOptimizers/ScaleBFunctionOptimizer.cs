using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class ScaleBFunctionOptimizer() : BaseMathFunctionOptimizer("ScaleB", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];
		var n = context.VisitedParameters[1];

		// ScaleB(x, 0) → x  (2^0 = 1, multiplication is identity)
		if (TryGetIntegerLiteral(n, out var nValue) && nValue == 0)
		{
			result = x;
			return true;
		}

		// ScaleB(0, n) → 0  (zero scaled by any power of 2 stays zero)
		if (TryGetNumericLiteral(x, out var xValue) && xValue == 0.0)
		{
			result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
			return true;
		}

		// For float/double: emit a fast IEEE 754 exponent-manipulation helper.
		// Benchmark (Apple M4 Pro, ARM64, .NET 10):
		//   Math.ScaleB  ≈ 0.55 ns  — hardware intrinsic
		//   FastScaleB   ≈ 0.68 ns  — bit-manipulation, useful on platforms without the intrinsic
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastScaleBMethodFloat(),
			SpecialType.System_Double => GenerateFastScaleBMethodDouble(),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: forward to the numeric helper type
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static bool TryGetIntegerLiteral(ExpressionSyntax expr, out int value)
	{
		value = 0;

		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: int i }:
			{
				value = i;
				return true;
			}
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: int i2 } }:
			{
				value = -i2;
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	/// <summary>
	///   Attempts to extract a numeric literal value from an expression.
	/// </summary>
	/// <param name="expr">The expression to inspect.</param>
	/// <param name="value">The extracted numeric value if successful.</param>
	/// <returns>True if the expression is a numeric literal; otherwise false.</returns>
	private static bool TryGetNumericLiteral(ExpressionSyntax expr, out double value)
	{
		value = 0;

		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: IConvertible c }:
			{
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			}
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
			{
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	private static string GenerateFastScaleBMethodFloat()
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast ScaleB for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 exponent manipulation when the exponent is in range, otherwise falls back to MathF.ScaleB.</remarks>")
			.WriteLine("/// <param name=\"x\">The value to scale.</param>")
			.WriteLine("/// <param name=\"n\">The power-of-two exponent.</param>")
			.WriteLine("/// <returns>The value scaled by 2^n.</returns>")
			.WriteLine("private static float FastScaleB(float x, int n)")
			.StartBlock()
			.WriteLine("if ((uint)(n + 126) <= 253u)")
			.StartBlock()
			.WriteLine("return x * BitConverter.Int32BitsToSingle((n + 127) << 23);")
			.EndBlock()
			.WriteLine("return MathF.ScaleB(x, n);")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastScaleBMethodDouble()
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast ScaleB for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses IEEE 754 exponent manipulation when the exponent is in range, otherwise falls back to Math.ScaleB.</remarks>")
			.WriteLine("/// <param name=\"x\">The value to scale.</param>")
			.WriteLine("/// <param name=\"n\">The power-of-two exponent.</param>")
			.WriteLine("/// <returns>The value scaled by 2^n.</returns>")
			.WriteLine("private static double FastScaleB(double x, int n)")
			.StartBlock()
			.WriteLine("if ((uint)(n + 1022) <= 2045u)")
			.StartBlock()
			.WriteLine("return x * BitConverter.UInt64BitsToDouble((ulong)((long)(n + 1023) << 52));")
			.EndBlock()
			.WriteLine("return Math.ScaleB(x, n);")
			.EndBlock();

		return builder.ToString();
	}
}