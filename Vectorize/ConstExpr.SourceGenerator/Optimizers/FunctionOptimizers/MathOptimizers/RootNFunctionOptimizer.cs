using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class RootNFunctionOptimizer() : BaseMathFunctionOptimizer("RootN", 2)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		var x = parameters[0];
		var n = parameters[1];

		// Try to get the n value if it's a literal
		if (TryGetIntegerLiteral(n, out var nValue))
		{
			// RootN(x, 1) => x
			if (nValue == 1)
			{
				result = x;
				return true;
			}

			// RootN(x, 2) => Sqrt(x)
			if (nValue == 2 && HasMethod(paramType, "Sqrt", 1))
			{
				result = CreateInvocation(paramType, "Sqrt", x);
				return true;
			}

			// RootN(x, 3) => Cbrt(x)
			if (nValue == 3 && HasMethod(paramType, "Cbrt", 1))
			{
				result = CreateInvocation(paramType, "Cbrt", x);
				return true;
			}

			// RootN(x, -1) => Reciprocal(x) or 1/x
			if (nValue == -1)
			{
				if (HasMethod(paramType, "Reciprocal", 1))
				{
					result = CreateInvocation(paramType, "Reciprocal", x);
					return true;
				}

				var div = SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
					SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0)), x);

				result = SyntaxFactory.ParenthesizedExpression(div);
				return true;
			}

			// For negative n: RootN(x, -n) => Reciprocal(RootN(x, n)) if available and fast-math, otherwise 1 / RootN(x, n)
			if (nValue < 0)
			{
				var positiveN = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
					SyntaxFactory.Literal(-nValue));

				var rootInvocation = CreateInvocation(paramType, "RootN", x, positiveN);

				if (HasMethod(paramType, "Reciprocal", 1))
				{
					result = CreateInvocation(paramType, "Reciprocal", rootInvocation);
					return true;
				}

				var div = SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
					SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0)),
					rootInvocation);

				result = SyntaxFactory.ParenthesizedExpression(div);
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastRootNMethodFloat()
				: GenerateFastRootNMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastRootN", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static bool TryGetIntegerLiteral(ExpressionSyntax expr, out int value)
	{
		value = 0;
		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: int i }:
				value = i;
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: int i2 } }:
				value = -i2;
				return true;
			default:
				return false;
		}
	}

	private static string GenerateFastRootNMethodFloat()
	{
		return """
			private static float FastRootN(float x, int n)
			{
				if (n == 0)
					return float.NaN;
				
				if (n == 1)
					return x;
				
				if (x == 0.0f)
					return 0.0f;
				
				if (n < 0)
					return 1.0f / FastRootN(x, -n);
				
				var absX = System.Math.Abs(x);
				
				// Initial approximation using bit manipulation
				var i = BitConverter.SingleToInt32Bits(absX);
				i = 0x3f800000 + (i - 0x3f800000) / n;
				var y = BitConverter.Int32BitsToSingle(i);
				
				// Newton-Raphson iteration: y = ((n-1)*y + x/y^(n-1)) / n
				var nMinus1 = n - 1;

				for (var iter = 0; iter < 3; iter++)
				{
					var yPow = 1.0f;

					for (var j = 0; j < nMinus1; j++)
						yPow *= y;
					
					y = (nMinus1 * y + absX / yPow) / n;
				}
				
				// Handle negative x for odd roots
				if (x < 0.0f && (n & 1) != 0)
					return -y;
				
				return y;
			}
			""";
	}

	private static string GenerateFastRootNMethodDouble()
	{
		return """
			private static double FastRootN(double x, int n)
			{
				if (n == 0)
					return double.NaN;
				
				if (n == 1)
					return x;
				
				if (x == 0.0)
					return 0.0;
				
				if (n < 0)
					return 1.0 / FastRootN(x, -n);
				
				var absX = System.Math.Abs(x);
				
				// Initial approximation using bit manipulation
				var i = BitConverter.DoubleToInt64Bits(absX);
				i = 0x3ff0000000000000L + (i - 0x3ff0000000000000L) / n;
				var y = BitConverter.Int64BitsToDouble(i);
				
				// Newton-Raphson iteration: y = ((n-1)*y + x/y^(n-1)) / n
				var nMinus1 = n - 1;

				for (var iter = 0; iter < 3; iter++)
				{
					var yPow = 1.0;

					for (var j = 0; j < nMinus1; j++)
						yPow *= y;
					
					y = (nMinus1 * y + absX / yPow) / n;
				}
				
				// Handle negative x for odd roots
				if (x < 0.0 && (n & 1) != 0)
					return -y;
				
				return y;
			}
			""";
	}
}
