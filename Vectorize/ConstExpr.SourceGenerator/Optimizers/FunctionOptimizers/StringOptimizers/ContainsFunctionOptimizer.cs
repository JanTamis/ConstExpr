using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes Contains calls:
/// - "hello".Contains("ell") → true
/// - "hello".Contains("world") → false
/// </summary>
public class ContainsFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Contains")
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
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
			result = Helpers.SyntaxHelpers.CreateLiteral(str.Contains(substring));
			return true;
		}

		if (literal.IsKind(SyntaxKind.CharacterLiteralExpression) && literal.Token.Value is char c)
		{
			result = Helpers.SyntaxHelpers.CreateLiteral(str.IndexOf(c) >= 0);
			return true;
		}

		return false;
	}
}

