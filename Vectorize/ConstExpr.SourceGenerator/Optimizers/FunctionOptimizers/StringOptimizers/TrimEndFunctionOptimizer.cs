using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

public class TrimEndFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "TrimEnd", false, n => n is 0)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		// Check if instance is already a TrimEnd call with no arguments
		if (Instance is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax } innerInvocation
		    && context.Model.TryGetSymbol(innerInvocation, context.SymbolStore, out IMethodSymbol? innerMethodSymbol)
		    && IsValidMethod(innerMethodSymbol, out _))
		{
			// s.TrimEnd().TrimEnd() → s.TrimEnd()
			result = innerInvocation;
			return true;
		}

		return false;
	}
}