using Microsoft.CodeAnalysis;
using System.Collections.Generic;

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

    // Compare the invocation syntax text (this is what actually matters for caching)
    // If the invocation text hasn't changed, we can reuse the cached result
    return x.Invocation.ToString() == y.Invocation.ToString()
			&& SymbolEqualityComparer.Default.Equals(x.MethodSymbol, y.MethodSymbol);
	}

	public int GetHashCode(InvocationModel? obj)
	{
		if (obj is null || obj.Invocation is null)
    {
      return 0;
    }

    // Use a hash based on the invocation text and method symbol
    unchecked
		{
			int hash = 17;
			hash = hash * 31 + obj.Invocation.ToString().GetHashCode();
			hash = hash * 31 + (obj.MethodSymbol != null 
				? SymbolEqualityComparer.Default.GetHashCode(obj.MethodSymbol) 
				: 0);
			return hash;
		}
	}
}
