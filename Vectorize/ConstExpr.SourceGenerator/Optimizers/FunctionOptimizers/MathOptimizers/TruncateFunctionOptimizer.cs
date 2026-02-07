using System;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class TruncateFunctionOptimizer() : BaseMathFunctionOptimizer("Truncate", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		// 1) Idempotence: Truncate(Truncate(x)) -> Truncate(x)
		if (parameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Truncate" } } innerInv)
		{
			result = innerInv;
			return true;
		}

		// 2) Integer types: Truncate(x) -> x (truncate has no effect on integers)
		if (paramType.IsNonFloatingNumeric())
		{
			result = parameters[0];
			return true;
		}

		// 3) Unary minus: Truncate(-x) -> -Truncate(x)
		if (parameters[0] is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix)
		{
			var truncateCall = CreateInvocation(paramType, "Truncate", prefix.Operand);

			result = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, SyntaxFactory.ParenthesizedExpression(truncateCall));
			return true;
		}

		// 4) Truncate(Floor(x)) -> Floor(x) (Floor already truncates towards negative infinity)
		if (parameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Floor" } } floorInv)
		{
			result = floorInv;
			return true;
		}

		// 5) Truncate(Ceiling(x)) -> Ceiling(x) (Ceiling already returns an integer)
		if (parameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Ceiling" } } ceilingInv)
		{
			result = ceilingInv;
			return true;
		}

		// 6) Truncate(Round(x)) -> Round(x) (Round already returns an integer value)
		if (parameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Round" } } roundInv)
		{
			result = roundInv;
			return true;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastTruncateMethodFloat()
				: GenerateFastTruncateMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastTruncate", parameters);
			return true;
		}

		// Default: keep as Truncate call (target numeric helper type)
		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastTruncateMethodFloat()
	{
		return """
			private static float FastTruncate(float x)
			{
				// Fast truncation using bit manipulation
				// This avoids the expensive conversion to int and back
				const uint signMask = 0x80000000u;
				const uint exponentMask = 0x7F800000u;
				const uint mantissaMask = 0x007FFFFFu;
				const int exponentBias = 127;
				
				var bits = BitConverter.SingleToUInt32Bits(x);
				var sign = bits & signMask;
				var exponent = (int)((bits & exponentMask) >> 23) - exponentBias;
				
				// Handle special cases
				if (exponent < 0)
					return BitConverter.UInt32BitsToSingle(sign); // Return +0.0f or -0.0f
				if (exponent >= 23)
					return x; // Already an integer or special value (Inf/NaN)
				
				// Clear fractional bits
				var mask = mantissaMask >> exponent;
				bits &= ~mask;
				
				return BitConverter.UInt32BitsToSingle(bits);
			}
			""";
	}

	private static string GenerateFastTruncateMethodDouble()
	{
		return """
			private static double FastTruncate(double x)
			{
				// Fast truncation using bit manipulation
				// This avoids the expensive conversion to long and back
				const ulong signMask = 0x8000000000000000ul;
				const ulong exponentMask = 0x7FF0000000000000ul;
				const ulong mantissaMask = 0x000FFFFFFFFFFFFFul;
				const int exponentBias = 1023;
				
				var bits = BitConverter.DoubleToUInt64Bits(x);
				var sign = bits & signMask;
				var exponent = (int)((bits & exponentMask) >> 52) - exponentBias;
				
				// Handle special cases
				if (exponent < 0)
					return BitConverter.UInt64BitsToDouble(sign); // Return +0.0 or -0.0
				if (exponent >= 52)
					return x; // Already an integer or special value (Inf/NaN)
				
				// Clear fractional bits
				var mask = mantissaMask >> exponent;
				bits &= ~mask;
				
				return BitConverter.UInt64BitsToDouble(bits);
			}
			""";
	}
}
