using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AtanFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Atan")
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

		// Expect one parameter for Atan
		if (parameters.Count != 1)
		{
			return false;
		}

		var x = parameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(x, out var value))
		{
			// Atan(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan(1) => π/4
			if (IsApproximately(value, 1.0))
			{
				var piOver4 = Math.PI / 4.0;
				result = SyntaxHelpers.CreateLiteral(piOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan(-1) => -π/4
			if (IsApproximately(value, -1.0))
			{
				var negPiOver4 = -Math.PI / 4.0;
				result = SyntaxHelpers.CreateLiteral(negPiOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		// When FastMath is enabled, add a fast atan approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast atan method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastAtanMethodFloat()
					: GenerateFastAtanMethodDouble();

				var fastAtanMethod = ParseMethodFromString(methodString);

				if (fastAtanMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastAtanMethod))
					{
						additionalMethods.Add(fastAtanMethod, false);
					}

					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastAtan"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));

					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Atan", x);
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

	private static string GenerateFastAtanMethodFloat()
	{
		return """
			private static float FastAtan(float x)
			{
				// Handle special cases
				if (Single.IsNaN(x)) return Single.NaN;
				if (Single.IsPositiveInfinity(x)) return Single.Pi / 2; // π/2
				if (Single.IsNegativeInfinity(x)) return -Single.Pi / 2; // -π/2

				var absX = Single.Abs(x);
				var sign = Single.Sign(x);
				
				var useReciprocal = absX > 1.0f;
				var z = useReciprocal ? Single.ReciprocalEstimate(absX) : absX;
				
				// Padé approximant coefficients (inlined)
				var z2 = z * z;
				var z4 = z2 * z2;
				
				// numerator = z * (15 + 4*z^2)
				var numerator = Single.FusedMultiplyAdd(4.0f, z2, 15.0f);
				numerator *= z;
				
				// denominator = 15 + 9*z^2 + z^4
				var denominator = Single.FusedMultiplyAdd(9.0f, z2, 15.0f);
				denominator = z4 + denominator;

				var result = numerator / denominator;
				
				// Adjust for reciprocal transformation
				if (useReciprocal)
				{
					result = Single.Pi / 2 - result; // π/2 - result
				}
				
				return sign * result;
			}
			""";
	}

	private static string GenerateFastAtanMethodDouble()
	{
		return """
			private static double FastAtan(double x)
			{
				// Handle special cases
				if (Double.IsNaN(x)) return Double.NaN;
				if (Double.IsPositiveInfinity(x)) return Double.Pi / 2; // π/2
				if (Double.IsNegativeInfinity(x)) return -Double.Pi / 2; // -π/2

				var absX = Double.Abs(x);
				var sign = Double.Sign(x);
				
				var useReciprocal = absX > 1.0;
				var z = useReciprocal ? Double.ReciprocalEstimate(absX) : absX;

				// Padé approximant coefficients (inlined)
				var z2 = z * z;
				var z4 = z2 * z2;
				
				// numerator = z * (15 + 4*z^2)
				var numerator = Double.FusedMultiplyAdd(4.0, z2, 15.0);
				numerator *= z;
				
				// denominator = 15 + 9*z^2 + z^4
				var denominator = Double.FusedMultiplyAdd(9.0, z2, 15.0);
				denominator = z4 + denominator;

				var result = numerator / denominator;
				
				// Adjust for reciprocal transformation
				if (useReciprocal)
				{
					result = Double.Pi / 2 - result; // π/2 - result
				}
				
				return sign * result;
			}
			""";
	}
}
