using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class ExpFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Exp")
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
			// For integers, cast to double and call Exp on double helper
			result = CreateInvocation(paramType, "Exp", parameters);
			return true;
		}

		// When FastMath is enabled, add chosen fast exp approximation methods
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Use order-3 polynomial for float (fastest option tested)
			// Use order-4 polynomial for double (fastest option tested)
			var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastExpMethodFloat_Order3()
					: GenerateFastExpMethodDouble_Order4();

			var fastExpMethod = ParseMethodFromString(methodString);

			if (fastExpMethod is not null)
			{
				if (!additionalMethods.ContainsKey(fastExpMethod))
				{
					additionalMethods.Add(fastExpMethod, false);
				}

				result = SyntaxFactory.InvocationExpression(
					SyntaxFactory.IdentifierName("FastExp"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));

				return true;
			}
		}

		// Default: keep as Exp call (target numeric helper type)
		result = CreateInvocation(paramType, "Exp", parameters);
		return true;
	}

	private static string GenerateFastExpMethodFloat_Order3()
	{
		return """
			private static float FastExp(float x)
			{
				// Safe bounds
				if (x >= 88.0f) return float.PositiveInfinity;
				if (x <= -87.0f) return 0.0f;

				const float LN2 = 0.6931471805599453f;
				const float INV_LN2 = 1.4426950408889634f;

				var kf = x * INV_LN2;
				var k = (int)(kf + (kf >= 0.0f ? 0.5f : -0.5f));
				var r = MathF.FusedMultiplyAdd(-k, LN2, x);

				// Order-3 Taylor: exp(r) ≈ 1 + r + r^2/2 + r^3/6
				var poly = 1.0f / 6.0f; // 1/6
				poly = MathF.FusedMultiplyAdd(poly, r, 0.5f); // -> 1/6*r + 1/2
				poly = MathF.FusedMultiplyAdd(poly, r, 1.0f); // -> (...)*r + 1
				var expR = MathF.FusedMultiplyAdd(poly, r, 1.0f);

				var bits = (k + 127) << 23;
				var scale = BitConverter.Int32BitsToSingle(bits);
				return scale * expR;
			}
			""";
	}

	private static string GenerateFastExpMethodDouble_Order4()
	{
		return """
			private static double FastExp(double x)
			{
				// Safe bounds
				if (x >= 709.0) return double.PositiveInfinity;
				if (x <= -708.0) return 0.0;

				const double LN2 = 0.6931471805599453094172321214581766;
				const double INV_LN2 = 1.4426950408889634073599246810018921;

				var kf = x * INV_LN2;
				var k = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));
				var r = System.Math.FusedMultiplyAdd(-k, LN2, x);

				// Order-4 Taylor: exp(r) ≈ 1 + r + r^2/2 + r^3/6 + r^4/24
				var poly = 1.0 / 24.0; // 1/24
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
