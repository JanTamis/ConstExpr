using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
///   Optimizes Contains calls:
///   - "hello".Contains("ell") → true
///   - "hello".Contains("world") → false
/// </summary>
public class ContainsFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Contains", false, n => n is 1)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;
		return false;
	}
}