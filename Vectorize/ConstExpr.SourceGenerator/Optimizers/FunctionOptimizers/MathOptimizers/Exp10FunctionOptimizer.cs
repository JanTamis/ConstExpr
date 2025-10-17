using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Exp10FunctionOptimizer() : BaseMathFunctionOptimizer("Exp10", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastExp10MethodFloat()
				: GenerateFastExp10MethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastExp10", parameters);
			return true;
		}

		// Default: keep as Exp10 call (target numeric helper type)
		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastExp10MethodFloat()
	{
		return """
			private static float FastExp10(float x)
			{
				// Preserve special cases like MathF.Pow does
				if (float.IsNaN(x)) return float.NaN;
				if (float.IsPositiveInfinity(x)) return float.PositiveInfinity;
				if (float.IsNegativeInfinity(x)) return 0.0f;
				if (x == 0.0f) return 1.0f; // handles +0 and -0

				// Safe bounds for 10^x to avoid overflow/underflow
				if (x >= 38.53f) return float.PositiveInfinity;
				if (x <= -38.53f) return 0.0f;

				const float LN10 = 2.302585092994046f;
				const float INV_LN2 = 1.4426950408889634f;

				// Compute y = x * ln(10) and use fast exp approximation on y
				var y = x * LN10;

				var kf = y * INV_LN2;
				var k = (int)(kf + (kf >= 0.0f ? 0.5f : -0.5f));
				var r = MathF.FusedMultiplyAdd(-k, 0.6931471805599453f, y);

				// Order-4 Taylor for exp(r): 1 + r + r^2/2 + r^3/6 + r^4/24
				var poly = 1.0f / 24.0f;
				poly = MathF.FusedMultiplyAdd(poly, r, 1.0f / 6.0f);
				poly = MathF.FusedMultiplyAdd(poly, r, 0.5f);
				poly = MathF.FusedMultiplyAdd(poly, r, 1.0f);
				var expR = MathF.FusedMultiplyAdd(poly, r, 1.0f);

				var bits = (k + 127) << 23;
				var scale = BitConverter.Int32BitsToSingle(bits);
				return scale * expR;
			}
			""";
	}

	private static string GenerateFastExp10MethodDouble()
	{
		return """
			private static double FastExp10(double x)
			{
				// Preserve special cases like Math.Pow does
				if (double.IsNaN(x)) return double.NaN;
				if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;
				if (double.IsNegativeInfinity(x)) return 0.0;
				if (x == 0.0) return 1.0; // handles +0 and -0

				// Safe bounds for 10^x to avoid overflow/underflow
				if (x >= 309.0) return double.PositiveInfinity;
				if (x <= -309.0) return 0.0;

				const double LN10 = 2.3025850929940456840179914546843642;
				const double INV_LN2 = 1.4426950408889634073599246810018921;

				var y = x * LN10;

				var kf = y * INV_LN2;
				var k = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));
				var r = System.Math.FusedMultiplyAdd(-k, 0.6931471805599453094172321214581766, y);

				// Order-4 Taylor for exp(r)
				var poly = 1.0 / 24.0;
				poly = System.Math.FusedMultiplyAdd(poly, r, 1.0 / 6.0);
				poly = System.Math.FusedMultiplyAdd(poly, r, 0.5);
				poly = System.Math.FusedMultiplyAdd(poly, r, 1.0);
				var expR = System.Math.FusedMultiplyAdd(poly, r, 1.0);

				var bits = (ulong)((k + 1023L) << 52);
				var scale = BitConverter.UInt64BitsToDouble(bits);
				return scale * expR;
			}
			""";
	}
}
