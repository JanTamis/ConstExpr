using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public abstract class BaseMathFunctionOptimizer(string name, params int[] parameterCounts) : BaseFunctionOptimizer
{
	public string Name { get; } = name;
	public int[] ParameterCounts { get; } = parameterCounts;	
	
	protected bool HasMethod(ITypeSymbol type, string name, int parameterCount)
	{
		return type.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(m => m.Parameters.Length == parameterCount
								&& m.DeclaredAccessibility == Accessibility.Public
								&& SymbolEqualityComparer.Default.Equals(type, m.ContainingType));
	}

	protected bool IsValidMathMethod(IMethodSymbol method, [NotNullWhen(true)] out ITypeSymbol type)
	{
		type = method.Parameters.Length > 0 ? method.Parameters[0].Type : null!;

		return method.Name == Name
			&& type.IsNumericType()
			&& ParameterCounts.Contains(method.Parameters.Length)
			&& method.ContainingType.ToString() is "System.Math" or "System.MathF" || method.ContainingType.EqualsType(type);
	}	

	protected bool IsApproximately(double a, double b)
	{
		return Math.Abs(a - b) <= double.Epsilon;
	}
}