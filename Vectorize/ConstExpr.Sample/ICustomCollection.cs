using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Sample;

public interface ICustomCollection<T>
{
	T Aggregate(Func<T, T, T> selector);
	
	void CopyTo(Span<T> data);
	
	IEnumerable<bool> Select(Func<T, bool> selector);
	
	bool SequenceEqual(IEnumerable<T> other);
	
	bool Contains(T element);
	
	bool ContainsAny(T element1, T element2, T element3, T element4);
	
	int CommonPrefixLength(ReadOnlySpan<T> other);
}