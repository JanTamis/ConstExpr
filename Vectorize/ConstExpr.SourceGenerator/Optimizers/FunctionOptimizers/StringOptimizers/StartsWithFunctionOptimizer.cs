using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes StartsWith calls:
/// - "hello".StartsWith("hel") → true
/// - "hello".StartsWith("world") → false
/// </summary>
public class StartsWithFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "StartsWith")
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
			var prefix = literal.Token.ValueText;
			result = Helpers.SyntaxHelpers.CreateLiteral(str.StartsWith(prefix));
			return true;
		}

		if (literal.IsKind(SyntaxKind.CharacterLiteralExpression) && literal.Token.Value is char c)
		{
			result = Helpers.SyntaxHelpers.CreateLiteral(str.Length > 0 && str[0] == c);
			return true;
		}

		return false;
	}
}

