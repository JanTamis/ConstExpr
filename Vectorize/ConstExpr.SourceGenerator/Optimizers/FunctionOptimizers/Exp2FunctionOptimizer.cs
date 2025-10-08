using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class Exp2FunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Exp2")
			return false;

		var containing = method.ContainingType?.ToString();
		var paramType = method.Parameters.Length > 0 ? method.Parameters[0].Type : null;
		var containingName = method.ContainingType?.Name;
		var paramTypeName = paramType?.Name;

		var isMath = containing is "System.Math" or "System.MathF";
		var isNumericHelper = paramTypeName is not null && containingName == paramTypeName;

		if (!isMath && !isNumericHelper || paramType is null)
			return false;

		if (!paramType.IsNumericType())
			return false;

		// Only meaningful for floating point types
		if (paramType.SpecialType is not (SpecialType.System_Single or SpecialType.System_Double))
		{
			// For integers, cast to double and call Exp2 on double helper
			result = CreateInvocation(paramType, "Exp2", parameters);
			return true;
		}

		// When FastMath is enabled, add chosen fast exp2 approximation methods
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastExp2MethodFloat_Order4()
					: GenerateFastExp2MethodDouble_Order4();

			var fastExpMethod = ParseMethodFromString(methodString);

			if (fastExpMethod is not null)
			{
				if (!additionalMethods.ContainsKey(fastExpMethod))
				{
					additionalMethods.Add(fastExpMethod, false);
				}

				result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastExp2"))
					.WithArgumentList(
						SyntaxFactory.ArgumentList(
							SyntaxFactory.SeparatedList(
								parameters.Select(SyntaxFactory.Argument))));

				return true;
			}
		}

		// Default: keep as Exp2 call (target numeric helper type)
		result = CreateInvocation(paramType, "Exp2", parameters);
		return true;
	}

	private static string GenerateFastExp2MethodFloat_Order4()
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

	private static string GenerateFastExp2MethodDouble_Order4()
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
