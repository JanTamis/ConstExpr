using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes redundant ToUpper/ToLower calls:
/// - s.ToUpper().ToUpper() → s.ToUpper()
/// - s.ToLower().ToLower() → s.ToLower()
/// - s.ToUpperInvariant().ToUpperInvariant() → s.ToUpperInvariant()
/// - s.ToLowerInvariant().ToLowerInvariant() → s.ToLowerInvariant()
/// </summary>
public class CaseFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "ToUpper")
{
	private static readonly HashSet<string> CaseMethods = new()
	{
		"ToUpper",
		"ToLower",
		"ToUpperInvariant",
		"ToLowerInvariant"
	};

	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		var methodName = method.Name;

		if (!CaseMethods.Contains(methodName))
		{
			return false;
		}

		if (method.IsStatic || parameters.Count > 0)
		{
			return false;
		}

		// Check if instance is already a case conversion call of the same type
		if (Instance is InvocationExpressionSyntax innerInvocation &&
		    innerInvocation.Expression is MemberAccessExpressionSyntax innerMemberAccess &&
		    innerMemberAccess.Name.Identifier.Text == methodName &&
		    innerInvocation.ArgumentList.Arguments.Count == 0)
		{
			// s.ToUpper().ToUpper() → s.ToUpper()
			result = innerInvocation;
			return true;
		}

		return false;
	}
}

