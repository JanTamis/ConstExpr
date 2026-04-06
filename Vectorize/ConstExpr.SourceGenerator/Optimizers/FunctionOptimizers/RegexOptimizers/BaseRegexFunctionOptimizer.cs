using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
/// Base class for optimizers that target <c>System.Text.RegularExpressions.Regex</c> methods.
/// Subclasses are discovered via reflection (same pattern as Math/Linq/Simd optimizers).
/// </summary>
public abstract class BaseRegexFunctionOptimizer(string name, params HashSet<int> parameterCounts) : BaseFunctionOptimizer
{
	public string Name { get; } = name;
	public HashSet<int> ParameterCounts { get; } = parameterCounts;

	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!IsRegexMethod(context.Method))
		{
			result = null;
			return false;
		}

		return TryOptimizeRegex(context, out result);
	}

	protected abstract bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result);

	private bool IsRegexMethod(IMethodSymbol method)
	{
		return method.Name == Name
		       && method.ContainingType.ToString() == "System.Text.RegularExpressions.Regex"
		       && ParameterCounts.Contains(method.Parameters.Length);
	}
}

