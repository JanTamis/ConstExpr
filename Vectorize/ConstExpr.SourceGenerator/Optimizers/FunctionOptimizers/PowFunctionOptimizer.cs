// filepath: /Users/jantamiskossen/RiderProjects/Vectorize/Vectorize/ConstExpr.SourceGenerator/Optimizers/FunctionOptimizers/PowFunctionOptimizer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class PowFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, ISet<SyntaxNode> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Pow")
		{
			return false;
		}

		var containing = method.ContainingType?.ToString();
		var paramType = method.Parameters.Length > 0 ? method.Parameters[0].Type : null;
		var containingName = method.ContainingType?.Name;
		var paramTypeName = paramType?.Name;

		var isMath = containing is "System.Math" or "System.MathF";
		var isNumericHelper = paramTypeName is not null && containingName == paramTypeName;

		if (!isMath && !isNumericHelper || paramType is null)
		{
			return false;
		}

		if (!paramType.IsNumericType())
		{
			return false;
		}

		// Expect two parameters for Pow
		if (parameters.Count != 2)
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
				var n = (int)Math.Round(exp);
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
				result = CreateInvocation(paramType, "RootN", x, SyntaxHelpers.CreateLiteral((int)Math.Round(1 / exp)));
				return true;
			}
		}

		// When FastMath is enabled, add a fast pow approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast pow method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastPowMethodFloat() 
					: GenerateFastPowMethodDouble();
					
				var fastPowMethod = ParseMethodFromString(methodString);
				
				if (fastPowMethod is not null)
				{
					additionalMethods.Add(fastPowMethod);
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastPow"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Pow", x, y);
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
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true; 
			default:
				return false;
		}
	}

	private static bool IsApproximately(double a, double b)
	{
		return Math.Abs(a - b) <= Double.Epsilon;
	}

	private static string GenerateFastPowMethodFloat()
	{
		return """
			private static float FastPow(float x, float y)
			{
				// Handle special cases
				if (y == 0.0f)
					return 1.0f;
					
				if (x == 0.0f)
					return 0.0f;
					
				if (x == 1.0f)
					return 1.0f;
				
				// Handle negative bases with non-integer exponents
				if (x < 0.0f && MathF.Abs(y - MathF.Round(y)) > float.Epsilon)
					return float.NaN;
				
				var isNegative = x < 0.0f;
				var absX = MathF.Abs(x);
				
				// Use bit manipulation for fast approximation: x^y ≈ 2^(y * log2(x))
				var bits = BitConverter.SingleToInt32Bits(absX);
				var exp = ((bits >> 23) & 0xFF) - 127;
				var mantissa = (bits & 0x7FFFFF) | 0x3F800000;
				var mantissaFloat = BitConverter.Int32BitsToSingle(mantissa);
				
				// Improved log2(mantissa) approximation for [1, 2) range
				var m = mantissaFloat;
				var log2Mantissa = -1.7417939f + (2.8212026f + (-1.4699568f + (0.4434793f - 0.0565717f * m) * m) * m) * m;
				var log2X = exp + log2Mantissa;
				
				// Calculate y * log2(x)
				var product = y * log2X;
				
				// Split into integer and fractional parts
				var intPart = MathF.Floor(product);
				var fracPart = product - intPart;
				
				// Better 2^fracPart approximation using exp2 polynomial
				var exp2Frac = 1.0f + fracPart * (0.693147f + fracPart * (0.240227f + fracPart * (0.0555041f + fracPart * (0.00961813f + fracPart * 0.00133336f))));
				
				// Combine: 2^product = 2^intPart * 2^fracPart
				var resultInt = (int)((intPart + 127) * (1 << 23));
				var exp2Int = BitConverter.Int32BitsToSingle(resultInt);
				var result = exp2Int * exp2Frac;
				
				// Handle negative base with odd integer exponent
				if (isNegative && MathF.Abs(y % 2.0f - 1.0f) < float.Epsilon)
					result = -result;
				
				return result;
			}
			""";
	}

	private static string GenerateFastPowMethodDouble()
	{
		return """
			private static double FastPow(double x, double y)
			{
				// Handle special cases
				if (y == 0.0)
					return 1.0;
					
				if (x == 0.0)
					return 0.0;
					
				if (x == 1.0)
					return 1.0;
				
				// Handle negative bases with non-integer exponents
				if (x < 0.0 && Math.Abs(y - Math.Round(y)) > double.Epsilon)
					return double.NaN;
				
				var isNegative = x < 0.0;
				var absX = Math.Abs(x);
				
				// Use bit manipulation for fast approximation: x^y ≈ 2^(y * log2(x))
				var bits = BitConverter.DoubleToInt64Bits(absX);
				var exp = ((bits >> 52) & 0x7FF) - 1023;
				var mantissa = (bits & 0xFFFFFFFFFFFFF) | 0x3FF0000000000000;
				var mantissaDouble = BitConverter.Int64BitsToDouble(mantissa);
				
				// Improved log2(mantissa) approximation for [1, 2) range
				// Using minimax polynomial approximation
				var m = mantissaDouble;
				var log2Mantissa = -1.7417939 + (2.8212026 + (-1.4699568 + (0.4434793 - 0.0565717 * m) * m) * m) * m;
				var log2X = exp + log2Mantissa;
				
				// Calculate y * log2(x)
				var product = y * log2X;
				
				// Split into integer and fractional parts for better accuracy
				var intPart = Math.Floor(product);
				var fracPart = product - intPart;
				
				// Better 2^fracPart approximation using exp2 polynomial for [0, 1)
				var exp2Frac = 1.0 + fracPart * (0.6931471805599453 + fracPart * (0.2402265069591007 + fracPart * (0.05550410866482158 + fracPart * (0.009618129842071888 + fracPart * 0.001333355814670307))));
				
				// Combine using bit manipulation for 2^intPart
				var resultLong = ((long)(intPart + 1023) << 52);
				if (resultLong < 0 || resultLong > (2047L << 52))
				{
					// Handle overflow/underflow
					return (resultLong < 0) ? 0.0 : double.PositiveInfinity;
				}
				
				var exp2Int = BitConverter.Int64BitsToDouble(resultLong);
				var result = exp2Int * exp2Frac;
				
				// Handle negative base with odd integer exponent
				if (isNegative && Math.Abs(y % 2.0 - 1.0) < double.Epsilon)
					result = -result;
				
				return result;
			}
			""";
	}
}
