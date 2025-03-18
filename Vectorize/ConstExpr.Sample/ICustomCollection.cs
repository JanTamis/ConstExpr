using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Sample;

public interface ICustomCollection<T>
{
	void CopyTo(Span<T> data);
	
	IEnumerable<TResult> Select<TResult>(Func<T, TResult> selector);
	
	bool SequenceEqual(IEnumerable<T> other);
}