using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

public class EndsWithFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "EndsWith")
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var stringType))
		{
			return false;
		}

		// Check for instance string
		if (!TryGetStringInstance(out var instanceString) 
		    || parameters.Count != 1)
		{
			return false;
		}

		if (method.Parameters[0].Type.SpecialType == SpecialType.System_Char)
		{
			if (String.IsNullOrEmpty(instanceString))
			{
				result = CreateLiteral(false);
				return true;
			}
			
			result = SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, CreateLiteral(instanceString[^1]), parameters[0]);
			return true;
		}

		return false;
	}
}