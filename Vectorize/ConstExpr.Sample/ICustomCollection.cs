using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace ConstExpr.SourceGenerator.Sample;

public interface ICustomCollection<T> : IEnumerable<T>
{
	IEnumerable<T> DistinctBy(Func<T, T> keySelector);
	
	bool Overlaps(IEnumerable<T> other);
	
	IEnumerable<(T, int)> Zip(IEnumerable<int> indices);
	
	// T Aggregate(Func<T, T, T> selector);
	//
	void CopyTo(Span<T> data);
	//
	// IEnumerable<bool> Select(Func<T, bool> selector);
	//
	// bool SequenceEqual(IEnumerable<T> other);
	
	bool Contains(T element);
	
	bool ContainsAnyInRange(T min, T max);

	bool ContainsAny(T element1, T element2, T element3);

	// int IndexOf(T item);

	void Replace(Span<T> destination, T oldValue, T newValue);
	
	// int Count(int element);

	TNumber Count<TNumber>(Func<T, bool> selector) where TNumber : INumber<TNumber>;
	
	// IEnumerable<KeyValuePair<int, TCount>> CountBy<TCount>() where TCount : INumber<TCount>;

	// int SequenceCompareTo(ReadOnlySpan<T> other);
	
	// bool ContainsAnyExcept(T element1, T element2, T element3, T element4);
	
	// bool EndsWith(T item);

	// int BinarySearch(T item);
	
	int CommonPrefixLength(ReadOnlySpan<T> other);
}

public interface ICharCollection
{
	IEnumerable<Rune> EnumerateRunes();
	
	IEnumerable<string> EnumerateLines();

	bool IsWhiteSpace();
}