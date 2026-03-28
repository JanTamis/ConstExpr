using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class RadiansToDegreesFunctionOptimizer() : BaseMathFunctionOptimizer("RadiansToDegrees", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
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
