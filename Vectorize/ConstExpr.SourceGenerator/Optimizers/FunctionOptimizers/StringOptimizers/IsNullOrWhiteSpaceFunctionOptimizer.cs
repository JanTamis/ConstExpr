using System;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes string.IsNullOrWhiteSpace(literal) to true/false.
/// </summary>
public class IsNullOrWhiteSpaceFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IsNullOrWhiteSpace")
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out _) || !method.IsStatic || parameters.Count != 1)
		{
			return false;
		}

		if (parameters[0] is not LiteralExpressionSyntax literal)
		{
			return false;
		}

		if (literal.IsKind(SyntaxKind.NullLiteralExpression))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		if (literal.IsKind(SyntaxKind.StringLiteralExpression))
		{
			var str = literal.Token.ValueText;
			result = SyntaxHelpers.CreateLiteral(string.IsNullOrWhiteSpace(str));
			return true;
		}

		return false;
	}
}

