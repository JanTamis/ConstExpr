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
public class TrimFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Trim")
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		var methodName = context.Method.Name;

		if (methodName is not ("Trim" or "TrimStart" or "TrimEnd"))
		{
			return false;
		}

		if (context.Method.IsStatic || context.VisitedParameters.Count > 0)
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

