using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class DegreesToRadiansFunctionOptimizer() : BaseMathFunctionOptimizer("DegreesToRadians", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
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