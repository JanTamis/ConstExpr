// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>A ref-struct list builder that uses an initial stack-allocated span and grows to the heap.</summary>
internal ref struct ValueListBuilder<T>
{
	private Span<T> _span;
	private T[]? _array;
	private int _pos;

	public ValueListBuilder(Span<T> initialSpan)
	{
		_span = initialSpan;
		_array = null;
		_pos = 0;
	}

	public int Length
	{
		get => _pos;
		set => _pos = value;
	}

	public ref T this[int index] => ref _span[index];

	public void Append(T item)
	{
		var pos = _pos;

		if ((uint) pos < (uint) _span.Length)
		{
			_span[pos] = item;
			_pos = pos + 1;
		}
		else
		{
			GrowAndAppend(item);
		}
	}

	public void Add(T item)
	{
		Append(item);
	}

	public T Pop()
	{
		var pos = --_pos;
		return _span[pos];
	}

	public ReadOnlySpan<T> AsSpan()
	{
		return _span.Slice(0, _pos);
	}

	public void Dispose()
	{
		_array = null;
	}

	private void GrowAndAppend(T item)
	{
		Grow();
		_span[_pos++] = item;
	}

	private void Grow()
	{
		var newArray = new T[Math.Max(_span.Length * 2, 4)];
		_span.Slice(0, _pos).CopyTo(newArray);
		_array = newArray;
		_span = newArray;
	}
}