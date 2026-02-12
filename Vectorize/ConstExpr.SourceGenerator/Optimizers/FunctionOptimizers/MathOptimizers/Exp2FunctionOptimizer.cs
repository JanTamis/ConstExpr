using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Exp2FunctionOptimizer() : BaseMathFunctionOptimizer("Exp2", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastExp2MethodFloat()
				: GenerateFastExp2MethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastExp2", context.VisitedParameters);
			return true;
		}

		// Default: keep as Exp2 call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastExp2MethodFloat()
	{
		return """
			private static float FastExp2(float x)
			{
				// Preserve special cases
				if (float.IsNaN(x)) return float.NaN;
				if (float.IsPositiveInfinity(x)) return float.PositiveInfinity;
				if (float.IsNegativeInfinity(x)) return 0.0f;
				if (x == 0.0f) return 1.0f; // handles +0 and -0

				// Safe bounds to avoid overflow/underflow
				if (x >= 128.0f) return float.PositiveInfinity;
				if (x <= -150.0f) return 0.0f;

				const float LN2 = 0.6931471805599453f;

				// Split x into integer k and fractional r: x = k + r
				var kf = x;
				var k = (int)(kf + (kf >= 0.0f ? 0.5f : -0.5f));
				var r = MathF.FusedMultiplyAdd(-k, 1.0f, x); // r = x - k

				// Compute exp(r * ln2) with order-4 Taylor
				var rln = r * LN2;

				var poly = 1.0f / 24.0f; // 1/24
				poly = MathF.FusedMultiplyAdd(poly, rln, 1.0f / 6.0f);
				poly = MathF.FusedMultiplyAdd(poly, rln, 0.5f);
				poly = MathF.FusedMultiplyAdd(poly, rln, 1.0f);
				var expR = MathF.FusedMultiplyAdd(poly, rln, 1.0f);

				var bits = (k + 127) << 23;
				var scale = BitConverter.Int32BitsToSingle(bits);
				return scale * expR;
			}
			""";
	}

	private static string GenerateFastExp2MethodDouble()
	{
		return """
			private static double FastExp2(double x)
			{
				// Preserve special cases
				if (double.IsNaN(x)) return double.NaN;
				if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;
				if (double.IsNegativeInfinity(x)) return 0.0;
				if (x == 0.0) return 1.0; // handles +0 and -0

				// Safe bounds to avoid overflow/underflow
				if (x >= 1024.0) return double.PositiveInfinity;
				if (x <= -1100.0) return 0.0;

				const double LN2 = 0.6931471805599453094172321214581766;

				var kf = x;
				var k = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));
				var r = System.Math.FusedMultiplyAdd(-k, 1.0, x); // r = x - k

				var rln = r * LN2;

				var poly = 1.0 / 24.0; // 1/24
				poly = System.Math.FusedMultiplyAdd(poly, rln, 1.0 / 6.0);
				poly = System.Math.FusedMultiplyAdd(poly, rln, 0.5);
				poly = System.Math.FusedMultiplyAdd(poly, rln, 1.0);
				var expR = System.Math.FusedMultiplyAdd(poly, rln, 1.0);

				var bits = (ulong)((k + 1023L) << 52);
				var scale = BitConverter.UInt64BitsToDouble(bits);
				return scale * expR;
			}
			""";
	}
}
