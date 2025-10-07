using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class TruncateFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Truncate")
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

		// When FastMath is enabled, add a fast truncate implementation
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast truncate method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastTruncateMethodFloat() 
					: GenerateFastTruncateMethodDouble();
					
				var fastTruncateMethod = ParseMethodFromString(methodString);
				
				if (fastTruncateMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastTruncateMethod))
					{
						additionalMethods.Add(fastTruncateMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastTruncate"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		// Default: keep as Truncate call (target numeric helper type)
		result = CreateInvocation(paramType, "Truncate", parameters[0]);
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
