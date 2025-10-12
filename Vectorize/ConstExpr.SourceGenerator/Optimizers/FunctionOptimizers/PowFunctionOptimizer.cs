using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class PowFunctionOptimizer() : BaseFunctionOptimizer("Pow", 2)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		var x = parameters[0];
		var y = parameters[1];

		// Algebraic simplifications on literal exponents (safe and type-preserving)
		if (TryGetNumericLiteral(y, out var exp))
		{
			// x^0 => 1
			if (IsApproximately(exp, 0.0))
			{
				result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0));
				return true;
			}

			// x^(-1) => Reciprocal(x) bij fast-math, anders 1/x
			if (IsApproximately(exp, -1.0) && IsPure(x))
			{
				if (floatingPointMode == FloatingPointEvaluationMode.FastMath && HasMethod(paramType, "Reciprocal", 1))
				{
					result = CreateInvocation(paramType, "Reciprocal", x);
					return true;
				}

				var div = SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
					SyntaxHelpers.CreateLiteral(1.0.ToSpecialType(paramType.SpecialType)), x);

				result = SyntaxFactory.ParenthesizedExpression(div);
				return true;
			}

			// x^n => x * x * ... * x for small integer n
			if (Math.Abs(exp) > 1.0 && Math.Abs(exp) <= 5.0 && IsPure(x) && Math.Abs(exp - Math.Round(exp)) < Double.Epsilon)
			{
				var n = (int) Math.Round(exp);
				var acc = x;

				for (var i = 1; i < Math.Abs(n); i++)
				{
					acc = SyntaxFactory.BinaryExpression(SyntaxKind.MultiplyExpression, acc, x);
				}

				if (n < 0)
				{
					acc = SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
						SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0)), acc);
				}

				result = SyntaxFactory.ParenthesizedExpression(acc);
				return true;
			}

			// x^1 => x
			if (IsApproximately(exp, 1.0))
			{
				result = x;
				return true;
			}

			// x^2 => (x * x) when x is pure (no side-effects)
			if (IsApproximately(exp, 2.0) && IsPure(x))
			{
				var mul = SyntaxFactory.BinaryExpression(SyntaxKind.MultiplyExpression, x, x);
				result = SyntaxFactory.ParenthesizedExpression(mul);
				return true;
			}

			// x^(1 / 2) => Sqrt(x)
			if (IsApproximately(exp, 1 / 2.0) && HasMethod(paramType, "Sqrt", 1))
			{
				result = CreateInvocation(paramType, "Sqrt", x);
				return true;
			}

			// x^(1 / 3) => Cbrt(x)
			if (IsApproximately(exp, 1 / 3.0) && HasMethod(paramType, "Cbrt", 1))
			{
				result = CreateInvocation(paramType, "Cbrt", x);
				return true;
			}

			// x^(1 / n) => RootN(x, n) for small integer n
			if (IsApproximately(1 / exp, Math.Floor(1 / exp)))
			{
				result = CreateInvocation(paramType, "RootN", x, SyntaxHelpers.CreateLiteral((int) Math.Round(1 / exp)));
				return true;
			}
		}

		// // When FastMath is enabled, add a fast pow approximation method
		// if (floatingPointMode == FloatingPointEvaluationMode.FastMath
		//     && paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		// {
		// 	var methodString = paramType.SpecialType == SpecialType.System_Single
		// 		? GenerateFastPowMethodFloat()
		// 		: GenerateFastPowMethodDouble();
		//
		// 	additionalMethods.TryAdd(ParseMethodFromString(methodString), false);
		//
		// 	result = CreateInvocation("FastPow", parameters);
		// 	return true;
		// }

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static bool TryGetNumericLiteral(ExpressionSyntax expr, out double value)
	{
		value = 0;

		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: IConvertible c }:
				value = c.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true;
			default:
				return false;
		}
	}

// 	private static string GenerateFastPowMethodFloat()
// 	{
// 		return """
// 			private static float FastPow(float x, float y)
// 			{
// 				// Handle special cases (keep minimal and predictable)
// 				if (y == 0.0f || x == 1.0f) return 1.0f;
// 				if (x <= 0.0f) return Single.NaN; // consistent with fast-math approximation path
//
// 				// Range reduction: x = m * 2^e with m in [1,2)
// 				var ibits = BitConverter.SingleToInt32Bits(x);
// 				var iexp = ((ibits >> 23) & 0xFF) - 127; // unbiased exponent
// 				var imant = ibits & 0x7FFFFF;
// 				var m = 1.0f + (imant * (1.0f / 8388608.0f)); // 2^23
//
// 				// log2(m) via ln(m) ≈ 2*(z + z^3/3 + z^5/5 + z^7/7), z = (m-1)/(m+1) ∈ [0, 1/3]
// 				const float INV_LN2 = 1.4426950408889634f; // 1/ln(2)
// 				var z = (m - 1.0f) / (m + 1.0f);
// 				var t2 = z * z; // z^2
// 				// Horner: z * (1 + t2/3 + t2^2/5 + t2^3/7)
// 				var sInner = Single.FusedMultiplyAdd(1f / 7f, t2, 1f / 5f);
// 				sInner = Single.FusedMultiplyAdd(sInner, t2, 1f / 3f);
// 				sInner = Single.FusedMultiplyAdd(sInner, t2, 1f);
// 				var ln_m = 2.0f * (z * sInner);
// 				var log2m = ln_m * INV_LN2;
// 				var log2x = iexp + log2m;
//
// 				var t = y * log2x;
//
// 				// exp2(t): split into k + f with f in [-0.5, 0.5) for better poly accuracy
// 				var kf = Single.Floor(t + 0.5f);
// 				var k = (int)kf;
// 				var f = t - kf;
//
// 				// 2^f ≈ e^(ln2*f), 7th order Taylor around 0
// 				const float LN2 = 0.6931471805599453f;
// 				var u = LN2 * f;
// 				// Horner with FMA: ((((((1/5040)u + 1/720)u + 1/120)u + 1/24)u + 1/6)u + 1/2)u + 1; then add final +u term fused
// 				var p = 1f / 5040f;
// 				p = Single.FusedMultiplyAdd(p, u, 1f / 720f);
// 				p = Single.FusedMultiplyAdd(p, u, 1f / 120f);
// 				p = Single.FusedMultiplyAdd(p, u, 1f / 24f);
// 				p = Single.FusedMultiplyAdd(p, u, 1f / 6f);
// 				p = Single.FusedMultiplyAdd(p, u, 0.5f);
// 				p = Single.FusedMultiplyAdd(p, u, 1f);
// 				var exp2f = Single.FusedMultiplyAdd(p, u, 1f);
//
// 				// Scale by 2^k via exponent bits (mantissa = 1.0)
// 				var expBits = (k + 127) << 23;
// 				var scale = BitConverter.Int32BitsToSingle(expBits);
// 				return exp2f * scale;
// 			}
// 			""";
// 	}
//
// 	private static string GenerateFastPowMethodDouble()
// 	{
// 		return """
// 			private static double FastPow(double x, double y)
// 			{
// 				// Handle special cases (keep minimal and predictable)
// 				if (y == 0.0 || x == 1.0) return 1.0;
// 				if (x <= 0.0) return Double.NaN;
//
// 				// Range reduction: x = m * 2^e with m in [1,2)
// 				var bits = BitConverter.DoubleToInt64Bits(x);
// 				var iexp = (int)((bits >> 52) & 0x7FF) - 1023; // unbiased exponent
// 				var imant = bits & 0x000F_FFFF_FFFF_FFFFL; // 52-bit mantissa
// 				var m = 1.0 + (imant * (1.0 / 4503599627370496.0)); // 2^52
//
// 				// log2(m) via ln(m) ≈ 2*(z + z^3/3 + z^5/5 + z^7/7)
// 				const double INV_LN2 = 1.4426950408889634073599; // 1/ln(2)
// 				var z = (m - 1.0) / (m + 1.0);
// 				var t2 = z * z;
// 				// Horner: z * (1 + t2/3 + t2^2/5 + t2^3/7)
// 				var sInner = Double.FusedMultiplyAdd(1.0 / 7.0, t2, 1.0 / 5.0);
// 				sInner = Double.FusedMultiplyAdd(sInner, t2, 1.0 / 3.0);
// 				sInner = Double.FusedMultiplyAdd(sInner, t2, 1.0);
// 				var ln_m = 2.0 * (z * sInner);
// 				var log2m = ln_m * INV_LN2;
// 				var log2x = iexp + log2m;
//
// 				var t = y * log2x;
//
// 				// exp2(t): k + f with f in [-0.5, 0.5)
// 				var kf = Double.Floor(t + 0.5);
// 				var k = (int)kf;
// 				var f = t - kf;
//
// 				// 2^f ≈ e^(ln2*f), 7th order Taylor
// 				const double LN2 = 0.6931471805599453094172;
// 				var u = LN2 * f;
// 				var p = 1.0 / 5040.0;
// 				p = Double.FusedMultiplyAdd(p, u, 1.0 / 720.0);
// 				p = Double.FusedMultiplyAdd(p, u, 1.0 / 120.0);
// 				p = Double.FusedMultiplyAdd(p, u, 1.0 / 24.0);
// 				p = Double.FusedMultiplyAdd(p, u, 1.0 / 6.0);
// 				p = Double.FusedMultiplyAdd(p, u, 0.5);
// 				p = Double.FusedMultiplyAdd(p, u, 1.0);
// 				var exp2f = Double.FusedMultiplyAdd(p, u, 1.0);
//
// 				// Scale by 2^k using exponent bits
// 				var expBits = ((long)(k + 1023) & 0x7FFL) << 52;
// 				var scale = BitConverter.Int64BitsToDouble(expBits);
// 				return exp2f * scale;
// 			}
// 			""";
// 	}
}