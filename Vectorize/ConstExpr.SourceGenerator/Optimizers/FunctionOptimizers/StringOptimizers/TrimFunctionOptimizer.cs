using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes redundant Trim calls:
/// - s.Trim().Trim() → s.Trim()
/// - s.TrimStart().TrimStart() → s.TrimStart()
/// - s.TrimEnd().TrimEnd() → s.TrimEnd()
/// </summary>
public class TrimFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Trim")
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		var methodName = method.Name;

		if (methodName is not ("Trim" or "TrimStart" or "TrimEnd"))
		{
			return false;
		}

		if (method.IsStatic || parameters.Count > 0)
		{
			return false;
		}

		// Check if instance is already a Trim call of the same type
		if (Instance is InvocationExpressionSyntax innerInvocation &&
		    innerInvocation.Expression is MemberAccessExpressionSyntax innerMemberAccess &&
		    innerMemberAccess.Name.Identifier.Text == methodName &&
		    innerInvocation.ArgumentList.Arguments.Count == 0)
		{
			// s.Trim().Trim() → s.Trim()
			result = innerInvocation;
			return true;
		}

		return false;
	}
}

