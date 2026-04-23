using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CopySignFunctionOptimizer() : BaseMathFunctionOptimizer("CopySign", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];
		var y = context.VisitedParameters[1];

		// 1) Unsigned integer: sign has no effect
		if (paramType.IsUnsignedInteger())
		{
			result = x;
			return true;
		}

		// 2) y is a known numeric literal: fold the sign statically
		if (TryGetNumericLiteral(y, out var signVal))
		{
			if (HasMethod(paramType, "Abs", 1))
			{
				if (signVal >= 0.0)
				{
					// CopySign(x, 0) → +|x|  and  CopySign(x, pos) → |x|
					result = CreateInvocation(paramType, "Abs", x);
					return true;
				}

				// CopySign(x, neg) → -|x|
				var absCall = CreateInvocation(paramType, "Abs", x);
				result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, absCall);
				return true;
			}
		}

		// 3) float: emit BitConverter bit-manipulation helper.
		//    Benchmark (ARM64 .NET 10, Apple M4 Pro):
		//      MathF.CopySign       ≈ 0.643 ns (baseline)
		//      CopySignFastFloat    ≈ 0.577 ns  → ~10% faster
		if (paramType.SpecialType == SpecialType.System_Single)
		{
			context.Usings.Add("System");

			context.AdditionalSyntax.TryAdd(
				ParseMethodFromString("""
					/// <summary>
					/// Bit-manipulation CopySign for float — ~10% faster than MathF.CopySign on ARM64.
					/// Masks the magnitude bits of x and the sign bit of y via BitConverter round-trip.
					/// </summary>
					private static float CopySignFastFloat(float x, float y)
					{
						var xBits = BitConverter.SingleToInt32Bits(x);
						var yBits = BitConverter.SingleToInt32Bits(y);
						return BitConverter.Int32BitsToSingle((xBits & 0x7FFF_FFFF) | (yBits & unchecked((int)0x8000_0000)));
					}
					"""),
				false);

			result = CreateInvocation("CopySignFastFloat", context.VisitedParameters);
			return true;
		}

		// 4) double: emit BitConverter bit-manipulation helper.
		//    Benchmark (ARM64 .NET 10, Apple M4 Pro):
		//      Math.CopySign        ≈ 0.637 ns (baseline)
		//      CopySignFastDouble   ≈ 0.576 ns  → ~10% faster
		if (paramType.SpecialType == SpecialType.System_Double)
		{
			context.Usings.Add("System");

			context.AdditionalSyntax.TryAdd(
				ParseMethodFromString("""
					/// <summary>
					/// Bit-manipulation CopySign for double — ~10% faster than Math.CopySign on ARM64.
					/// Masks the magnitude bits of x and the sign bit of y via BitConverter round-trip.
					/// </summary>
					private static double CopySignFastDouble(double x, double y)
					{
						var xBits = BitConverter.DoubleToInt64Bits(x);
						var yBits = BitConverter.DoubleToInt64Bits(y);
						return BitConverter.Int64BitsToDouble((xBits & long.MaxValue) | (yBits & long.MinValue));
					}
					"""),
				false);

			result = CreateInvocation("CopySignFastDouble", context.VisitedParameters);
			return true;
		}

		// Default: forward to target numeric helper type
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static bool TryGetNumericLiteral(ExpressionSyntax expr, out double value)
	{
		value = 0;

		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: IConvertible c }:
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			default:
				return false;
		}
	}
}