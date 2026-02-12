using System;
﻿using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class ExpFunctionOptimizer() : BaseMathFunctionOptimizer("Exp", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		var arg = context.VisitedParameters[0];

		// Exp(Log(x)) => x (inverse operation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Log" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			// Use order-3 polynomial for float (fastest option tested)
			// Use order-4 polynomial for double (fastest option tested)
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastExpMethodFloat()
				: GenerateFastExpMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastExp", context.VisitedParameters);
			return true;
		}

		// Default: keep as Exp call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastExpMethodFloat()
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

	private static string GenerateFastExpMethodDouble()
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
