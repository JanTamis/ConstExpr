using System;
﻿using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class RadiansToDegreesFunctionOptimizer() : BaseMathFunctionOptimizer("RadiansToDegrees", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		// RadiansToDegrees(x) = x * (180 / π)
		// Add optimized conversion context.Method
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateRadiansToDegreesMethodFloat()
				: GenerateRadiansToDegreesMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastRadiansToDegrees", context.VisitedParameters);
			return true;
		}

		// For other numeric types, fall back to standard context.Method call
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateRadiansToDegreesMethodFloat()
	{
		return """
			private static float FastRadiansToDegrees(float radians)
			{
				// radians * (180 / π)
				// Using precise constant: 180 / π = 57.29577951308232
				const float RadToDeg = 57.29578f;
				return radians * RadToDeg;
			}
			""";
	}

	private static string GenerateRadiansToDegreesMethodDouble()
	{
		return """
			private static double FastRadiansToDegrees(double radians)
			{
				// radians * (180 / π)
				// Using precise constant: 180 / π = 57.29577951308232
				const double RadToDeg = 57.29577951308232;
				return radians * RadToDeg;
			}
			""";
	}
}
