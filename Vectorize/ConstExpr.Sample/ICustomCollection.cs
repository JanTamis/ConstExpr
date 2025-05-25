using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Microsoft.Diagnostics.Symbols;

namespace ConstExpr.SourceGenerator.Sample;

public interface ICustomCollection<T>
{
	// T Aggregate(Func<T, T, T> selector);
	//
	void CopyTo(Span<T> data);
	//
	// IEnumerable<bool> Select(Func<T, bool> selector);
	//
	// bool SequenceEqual(IEnumerable<T> other);
	
	bool Contains(T element);
	
	bool ContainsAnyInRange(T min, T max);

	int IndexOf(T item);

	void Replace(Span<T> destination, T oldValue, T newValue);
	
	int Count(int element);
	
	IEnumerable<KeyValuePair<TKey, TCount>> CountBy<TKey, TCount>(Func<T, TKey> keySelector) where TCount: INumber<TCount>;

	// int SequenceCompareTo(ReadOnlySpan<T> other);
	
	// bool ContainsAny(T element1, T element2, T element3, T element4);
	// bool ContainsAnyExcept(T element1, T element2, T element3, T element4);
	
	// bool EndsWith(T item);

	// int BinarySearch(T item);
	
	// int CommonPrefixLength(ReadOnlySpan<T> other);
}

public interface ICharCollection
{
	IEnumerable<Rune> EnumerateRunes();
	
	IEnumerable<string> EnumerateLines();

	
	
	bool IsWhiteSpace();
}