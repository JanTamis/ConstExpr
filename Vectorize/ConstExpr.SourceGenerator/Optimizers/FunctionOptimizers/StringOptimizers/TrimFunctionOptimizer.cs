using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes redundant Trim calls:
/// - s.Trim().Trim() → s.Trim()
/// - s.TrimStart().TrimStart() → s.TrimStart()
/// - s.TrimEnd().TrimEnd() → s.TrimEnd()
/// </summary>
public class TrimFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Trim", false, 1)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		var methodName = context.Method.Name;

		if (methodName is not ("Trim" or "TrimStart" or "TrimEnd"))
		{
			return false;
		}

		// Check if instance is already a Trim call of the same type
		if (Instance is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax innerMemberAccess } innerInvocation 
		    && innerMemberAccess.Name.Identifier.Text == methodName 
		    && innerInvocation.ArgumentList.Arguments.Count == 0)
		{
			// s.Trim().Trim() → s.Trim()
			result = innerInvocation;
			return true;
		}

		return false;
	}
}

