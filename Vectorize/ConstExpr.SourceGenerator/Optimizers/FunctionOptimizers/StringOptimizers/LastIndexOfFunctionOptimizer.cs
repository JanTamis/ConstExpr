using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes LastIndexOf calls:
/// - "hello".LastIndexOf("l") → 3
/// - "hello".LastIndexOf("world") → -1
/// </summary>
public class LastIndexOfFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "LastIndexOf")
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out _) || method.IsStatic || parameters.Count < 1)
		{
			return false;
		}

		if (!TryGetStringInstance(out var str) || str is null)
		{
			return false;
		}

		if (parameters[0] is not LiteralExpressionSyntax literal)
		{
			return false;
		}

		if (literal.IsKind(SyntaxKind.StringLiteralExpression))
		{
			var substring = literal.Token.ValueText;
			result = Helpers.SyntaxHelpers.CreateLiteral(str.LastIndexOf(substring, StringComparison.Ordinal));
			return true;
		}

		if (literal.IsKind(SyntaxKind.CharacterLiteralExpression) && literal.Token.Value is char c)
		{
			result = Helpers.SyntaxHelpers.CreateLiteral(str.LastIndexOf(c));
			return true;
		}

		return false;
	}
}

