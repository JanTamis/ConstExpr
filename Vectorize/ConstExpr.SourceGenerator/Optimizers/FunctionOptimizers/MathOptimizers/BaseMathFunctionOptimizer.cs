using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public abstract class BaseMathFunctionOptimizer(string name, params HashSet<int> parameterCounts) : BaseFunctionOptimizer
{
	protected abstract bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result);

	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			result = null;
			return false;
		}

		return TryOptimizeMath(context, paramType, out result);
	}

	public string Name { get; } = name;
	public HashSet<int> ParameterCounts { get; } = parameterCounts;

	protected bool HasMethod(ITypeSymbol type, string name, int parameterCount)
	{
		return type.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(m => m.Parameters.Length == parameterCount
								&& m.DeclaredAccessibility == Accessibility.Public
								&& SymbolEqualityComparer.Default.Equals(type, m.ContainingType));
	}

	protected bool IsApproximately(double a, double b)
	{
		return Math.Abs(a - b) <= double.Epsilon;
	}

	private bool IsValidMathMethod(IMethodSymbol method, [NotNullWhen(true)] out ITypeSymbol? type)
	{
		type = method.Parameters
			.Select(s => s.Type)
			.FirstOrDefault();

		return method.Name == Name
		       && ParameterCounts.Contains(method.Parameters.Length);
	}
}