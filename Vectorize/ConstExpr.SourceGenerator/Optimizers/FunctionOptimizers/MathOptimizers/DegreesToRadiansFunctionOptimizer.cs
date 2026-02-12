using System;
﻿using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class DegreesToRadiansFunctionOptimizer() : BaseMathFunctionOptimizer("DegreesToRadians", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		// DegreesToRadians(x) = x * (π / 180)
		// Add optimized conversion context.Method
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateDegreesToRadiansMethodFloat()
				: GenerateDegreesToRadiansMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastDegreesToRadians", context.VisitedParameters);
			return true;
		}

		// For other numeric types, fall back to standard context.Method call
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateDegreesToRadiansMethodFloat()
	{
		return """
			private static float FastDegreesToRadians(float degrees)
			{
				// degrees * (π / 180)
				// Using precise constant: π / 180 = 0.017453292519943295
				const float DegToRad = 0.017453292f;
				return degrees * DegToRad;
			}
			""";
	}

	private static string GenerateDegreesToRadiansMethodDouble()
	{
		return """
			private static double FastDegreesToRadians(double degrees)
			{
				// degrees * (π / 180)
				// Using precise constant: π / 180 = 0.017453292519943295
				const double DegToRad = 0.017453292519943295;
				return degrees * DegToRad;
			}
			""";
	}
}
