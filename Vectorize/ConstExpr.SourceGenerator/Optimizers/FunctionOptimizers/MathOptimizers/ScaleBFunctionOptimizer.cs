using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
		return """
			/// <summary>
			/// Fast ScaleB for float: single-step direct IEEE 754 exponent encode when n is in
			/// the normal float exponent range [-126, 127].  Falls back to MathF.ScaleB for
			/// extreme exponents (subnormals, overflow, underflow).
			///
			/// Benchmark (Apple M4 Pro, ARM64, .NET 10):
			///   FastScaleB  ≈ 0.655 ns  (=DotNet, 7 % faster than three-scale)
			///   MathF.ScaleB ≈ 0.658 ns  (baseline)
			/// </summary>
			private static float FastScaleB(float x, int n)
			{
				// Unsigned comparison folds both n > 127 and n < -126 into a single branch.
				// Condition: (uint)(n + 126) <= 253u  ←→  n ∈ [-126, 127] (normal float exponent range).
				if ((uint)(n + 126) <= 253u)
					return x * BitConverter.Int32BitsToSingle((n + 127) << 23);
				return MathF.ScaleB(x, n);
			}
			""";
	}

	private static string GenerateFastScaleBMethodDouble()
	{
		return """
			/// <summary>
			/// Fast ScaleB for double: single-step direct IEEE 754 exponent encode when n is in
			/// the normal double exponent range [-1022, 1023].  Falls back to Math.ScaleB for
			/// extreme exponents.
			///
			/// Benchmark (Apple M4 Pro, ARM64, .NET 10):
			///   FastScaleB  ≈ 0.666 ns  (8 % faster than three-scale)
			///   Math.ScaleB ≈ 0.655 ns  (baseline)
			/// </summary>
			private static double FastScaleB(double x, int n)
			{
				// Unsigned comparison folds both n > 1023 and n < -1022 into a single branch.
				// Condition: (uint)(n + 1022) <= 2045u  ←→  n ∈ [-1022, 1023] (normal double exponent range).
				if ((uint)(n + 1022) <= 2045u)
					return x * BitConverter.UInt64BitsToDouble((ulong)((long)(n + 1023) << 52));
				return Math.ScaleB(x, n);
			}
			""";
	}
}