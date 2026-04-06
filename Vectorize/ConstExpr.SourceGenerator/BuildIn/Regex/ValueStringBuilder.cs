// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>A ref-struct string builder that uses an initial stack-allocated span and grows to the heap.</summary>
internal ref struct ValueStringBuilder
{
	private char[]? _arrayToReturnToPool;
	private Span<char> _chars;
	private int _pos;

	public ValueStringBuilder(Span<char> initialBuffer)
	{
		_arrayToReturnToPool = null;
		_chars = initialBuffer;
		_pos = 0;
	}

	public ValueStringBuilder(int initialCapacity)
	{
		_arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
		_chars = _arrayToReturnToPool;
		_pos = 0;
	}

	public int Length
	{
		get => _pos;
		set
		{
			Debug.Assert(value >= 0);
			Debug.Assert(value <= _chars.Length);
			_pos = value;
		}
	}

	public int Capacity => _chars.Length;

	public ref char this[int index]
	{
		get
		{
			Debug.Assert(index < _pos);
			return ref _chars[index];
		}
	}

	public override string ToString()
	{
		var s = _chars.Slice(0, _pos).ToString();
		Dispose();
		return s;
	}

	public ReadOnlySpan<char> AsSpan()
	{
		return _chars.Slice(0, _pos);
	}

	public ReadOnlySpan<char> AsSpan(int start)
	{
		return _chars.Slice(start, _pos - start);
	}

	public ReadOnlySpan<char> AsSpan(int start, int length)
	{
		return _chars.Slice(start, length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Span<char> AppendSpan(int length)
	{
		var origPos = _pos;
		if (origPos > _chars.Length - length)
			Grow(length);
		_pos = origPos + length;
		return _chars.Slice(origPos, length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Append(char c)
	{
		var pos = _pos;

		if ((uint) pos < (uint) _chars.Length)
		{
			_chars[pos] = c;
			_pos = pos + 1;
		}
		else
		{
			GrowAndAppend(c);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Append(string? s)
	{
		if (s == null)
			return;

		var pos = _pos;

		if (s.Length == 1 && (uint) pos < (uint) _chars.Length)
		{
			_chars[pos] = s[0];
			_pos = pos + 1;
		}
		else
		{
			AppendSlow(s);
		}
	}

	private void AppendSlow(string s)
	{
		var pos = _pos;
		if (pos > _chars.Length - s.Length)
			Grow(s.Length);

		s.AsSpan().CopyTo(_chars.Slice(pos));
		_pos += s.Length;
	}

	public void Append(ReadOnlySpan<char> value)
	{
		var pos = _pos;
		if (pos > _chars.Length - value.Length)
			Grow(value.Length);

		value.CopyTo(_chars.Slice(_pos));
		_pos += value.Length;
	}

	public void Append(char c, int count)
	{
		if (_pos > _chars.Length - count)
			Grow(count);

		var dst = _chars.Slice(_pos, count);

		for (var i = 0; i < dst.Length; i++)
		{
			dst[i] = c;
		}
		_pos += count;
	}

	public void Insert(int index, string? s)
	{
		if (s == null)
			return;

		var count = s.Length;
		if (_pos > _chars.Length - count)
			Grow(count);

		var remaining = _pos - index;
		_chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
		s.AsSpan().CopyTo(_chars.Slice(index));
		_pos += count;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void GrowAndAppend(char c)
	{
		Grow(1);
		Append(c);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void Grow(int additionalCapacityBeyondPos)
	{
		Debug.Assert(additionalCapacityBeyondPos > 0);

		var newCapacity = Math.Max(_pos + additionalCapacityBeyondPos, _chars.Length * 2);
		var poolArray = ArrayPool<char>.Shared.Rent(newCapacity);
		_chars.Slice(0, _pos).CopyTo(poolArray);

		var toReturn = _arrayToReturnToPool;
		_chars = _arrayToReturnToPool = poolArray;
		if (toReturn != null)
			ArrayPool<char>.Shared.Return(toReturn);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Dispose()
	{
		var toReturn = _arrayToReturnToPool;
		this = default;
		if (toReturn != null)
			ArrayPool<char>.Shared.Return(toReturn);
	}
}