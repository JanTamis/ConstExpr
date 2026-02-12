using System;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

public class EndsWithFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "EndsWith")
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(context.Method, out var stringType))
		{
			return false;
		}

		// Check for instance string
		if (!TryGetStringInstance(out var instanceString) 
		    || context.VisitedParameters.Count != 1)
		{
			return false;
		}

		if (context.Method.Parameters[0].Type.SpecialType == SpecialType.System_Char)
		{
			if (String.IsNullOrEmpty(instanceString))
			{
				result = CreateLiteral(false);
				return true;
			}
			
			result = SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, CreateLiteral(instanceString[^1]), context.VisitedParameters[0]);
			return true;
		}

		return false;
	}
}