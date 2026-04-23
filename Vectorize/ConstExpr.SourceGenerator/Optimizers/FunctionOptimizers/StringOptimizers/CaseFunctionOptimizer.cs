using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes redundant ToUpper/ToLower calls:
/// - s.ToUpper().ToUpper() → s.ToUpper()
/// - s.ToLower().ToLower() → s.ToLower()
/// - s.ToUpperInvariant().ToUpperInvariant() → s.ToUpperInvariant()
/// - s.ToLowerInvariant().ToLowerInvariant() → s.ToLowerInvariant()
/// </summary>
public class CaseFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "ToUpper", false, n => n is 0)
{
	private static readonly HashSet<string> CaseMethods =
	[
		"ToUpper",
		"ToLower",
		"ToUpperInvariant",
		"ToLowerInvariant"
	];

	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		var methodName = context.Method.Name;

		if (!CaseMethods.Contains(methodName))
		{
			return false;
		}

		// Check if instance is already a case conversion call of the same type
		if (Instance is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax innerMemberAccess } innerInvocation
		    && innerMemberAccess.Name.Identifier.Text == methodName
		    && innerInvocation.ArgumentList.Arguments.Count == 0)
		{
			// s.ToUpper().ToUpper() → s.ToUpper()
			result = innerInvocation;
			return true;
		}

		return false;
	}
}