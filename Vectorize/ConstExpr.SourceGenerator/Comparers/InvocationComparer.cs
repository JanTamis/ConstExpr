using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Comparers;

/// <summary>
/// Comparer for InvocationModel to enable incremental generation caching.
/// Only processes invocations that have actually changed.
/// </summary>
public class InvocationModelEqualityComparer : IEqualityComparer<InvocationModel?>
{
	public static readonly InvocationModelEqualityComparer Instance = new();

	public bool Equals(InvocationModel? x, InvocationModel? y)
	{
		if (ReferenceEquals(x, y))
    {
      return true;
    }

    if (x is null || y is null)
    {
      return false;
    }

    if (x.Invocation is null || y.Invocation is null)
    {
      return false;
    }

		return string.Equals(x.CacheKey, y.CacheKey, System.StringComparison.Ordinal)
			&& SymbolEqualityComparer.Default.Equals(x.MethodSymbol, y.MethodSymbol)
			&& x.AttributeData?.MathOptimizations == y.AttributeData?.MathOptimizations
			&& x.AttributeData?.LinqOptimisationMode == y.AttributeData?.LinqOptimisationMode;
	}

	public int GetHashCode(InvocationModel? obj)
	{
		if (obj?.Invocation is null)
    {
      return 0;
    }

    unchecked
		{
			var hash = 17;
      hash = hash * 31 + (obj.CacheKey?.GetHashCode() ?? 0);
			hash = hash * 31 + (obj.MethodSymbol != null 
				? SymbolEqualityComparer.Default.GetHashCode(obj.MethodSymbol) 
				: 0);
			hash = hash * 31 + (obj.AttributeData?.MathOptimizations.GetHashCode() ?? 0);
			hash = hash * 31 + (obj.AttributeData?.LinqOptimisationMode.GetHashCode() ?? 0);
			return hash;
		}
	}
}
