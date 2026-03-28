using System;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

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
		result = paramType.SpecialType switch
		{
			SpecialType.System_Single => MultiplyExpression(context.VisitedParameters[0], CreateLiteral(180 / MathF.PI)),
			SpecialType.System_Double => MultiplyExpression(context.VisitedParameters[0], CreateLiteral(180 / Math.PI)),
			_ => CreateInvocation(paramType, Name, context.VisitedParameters)
		};
		
		return true;
	}
}
