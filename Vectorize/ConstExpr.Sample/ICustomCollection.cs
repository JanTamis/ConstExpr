using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Sample;

public interface ICustomCollection<T>
{
	void CopyTo(Span<T> data);
	
	IEnumerable<bool> Select(Func<T, bool> selector);
	
	bool SequenceEqual(IEnumerable<T> other);
}