using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

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
		result = paramType.SpecialType switch
		{
			SpecialType.System_Single => MultiplyExpression(context.VisitedParameters[0], CreateLiteral(MathF.PI / 180)),
			SpecialType.System_Double => MultiplyExpression(context.VisitedParameters[0], CreateLiteral(Math.PI / 180)),
			_ => CreateInvocation(paramType, Name, context.VisitedParameters)
		};

		return true;
	}
}