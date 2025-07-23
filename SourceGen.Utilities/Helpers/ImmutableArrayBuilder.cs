using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace SourceGen.Utilities.Helpers;

/// <summary>
/// A helper type to build sequences of values with pooled buffers.
/// </summary>
/// <typeparam name="T">The type of items to create sequences for.</typeparam>
public struct ImmutableArrayBuilder<T> : IDisposable
{
	/// <summary>
	/// The shared <see cref="ObjectPool{T}"/> instance to share <see cref="Writer"/> objects.
	/// </summary>
	private static readonly ObjectPool<Writer> SharedObjectPool = new(static () => new Writer());

	/// <summary>
	/// The rented <see cref="Writer"/> instance to use.
	/// </summary>
	private Writer? _writer;

	/// <summary>
	/// Creates a new <see cref="ImmutableArrayBuilder{T}"/> object.
	/// </summary>
	public ImmutableArrayBuilder()
	{
		_writer = SharedObjectPool.Allocate();
	}

	/// <summary>
	/// Gets the data written to the underlying buffer so far, as a <see cref="ReadOnlySpan{T}"/>.
	/// </summary>
	public readonly ReadOnlySpan<T> WrittenSpan
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _writer!.WrittenSpan;
	}

	/// <summary>
	/// Gets the number of elements currently written in the current instance.
	/// </summary>
	public readonly int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _writer!.Count;
	}

	/// <summary>
	/// Advances the current writer and gets a <see cref="Span{T}"/> to the requested memory area.
	/// </summary>
	/// <param name="requestedSize">The requested size to advance by.</param>
	/// <returns>A <see cref="Span{T}"/> to the requested memory area.</returns>
	/// <remarks>
	/// No other data should be written to the builder while the returned <see cref="Span{T}"/>
	/// is in use, as it could invalidate the memory area wrapped by it, if resizing occurs.
	/// </remarks>
	public readonly Span<T> Advance(int requestedSize)
	{
		return _writer!.Advance(requestedSize);
	}

	/// <inheritdoc cref="ImmutableArray{T}.Builder.Add(T)"/>
	public readonly void Add(T item)
	{
		_writer!.Add(item);
	}

	/// <summary>
	/// Adds the specified items to the end of the array.
	/// </summary>
	/// <param name="items">The items to add at the end of the array.</param>
	public readonly void AddRange(ReadOnlySpan<T> items)
	{
		_writer!.AddRange(items);
	}

	/// <inheritdoc cref="ImmutableArray{T}.Builder.Clear"/>
	public readonly void Clear()
	{
		_writer!.Clear();
	}

	/// <summary>
	/// Inserts an item to the builder at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
	/// <param name="item">The object to insert into the current instance.</param>
	public readonly void Insert(int index, T item)
	{
		_writer!.Insert(index, item);
	}

	/// <summary>
	/// Gets an <see cref="IEnumerable{T}"/> instance for the current builder.
	/// </summary>
	/// <returns>An <see cref="IEnumerable{T}"/> instance for the current builder.</returns>
	/// <remarks>
	/// The builder should not be mutated while an enumerator is in use.
	/// </remarks>
	public readonly IEnumerable<T> AsEnumerable()
	{
		return _writer!;
	}

	/// <inheritdoc cref="ImmutableArray{T}.Builder.ToImmutable"/>
	public readonly ImmutableArray<T> ToImmutable()
	{
		var array = _writer!.WrittenSpan.ToArray();

		return Unsafe.As<T[], ImmutableArray<T>>(ref array);
	}

	/// <inheritdoc cref="ImmutableArray{T}.Builder.ToArray"/>
	public readonly T[] ToArray()
	{
		return _writer!.WrittenSpan.ToArray();
	}

	/// <inheritdoc/>
	public override readonly string ToString()
	{
		return _writer!.WrittenSpan.ToString();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		var writer = _writer;

		_writer = null;

		if (writer is not null)
		{
			writer.Clear();

			SharedObjectPool.Free(writer);
		}
	}

	/// <summary>
	/// A class handling the actual buffer writing.
	/// </summary>
	private sealed class Writer : IList<T>, IReadOnlyList<T>
	{
		/// <summary>
		/// The underlying <typeparamref name="T"/> array.
		/// </summary>
		private T[] array;

		/// <summary>
		/// The starting offset within <see cref="array"/>.
		/// </summary>
		private int index;

		/// <summary>
		/// Creates a new <see cref="Writer"/> instance with the specified parameters.
		/// </summary>
		public Writer()
		{
			if (typeof(T) == typeof(char))
			{
				array = new T[1024];
			}
			else
			{
				array = new T[8];
			}

			index = 0;
		}

		/// <inheritdoc/>
		public int Count => index;

		/// <inheritdoc cref="ImmutableArrayBuilder{T}.WrittenSpan"/>
		public ReadOnlySpan<T> WrittenSpan
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new(array, 0, index);
		}

		/// <inheritdoc/>
		bool ICollection<T>.IsReadOnly => true;

		/// <inheritdoc/>
		T IReadOnlyList<T>.this[int index] => WrittenSpan[index];

		/// <inheritdoc/>
		T IList<T>.this[int index]
		{
			get => WrittenSpan[index];
			set => throw new NotSupportedException();
		}

		/// <inheritdoc cref="ImmutableArrayBuilder{T}.Advance"/>
		public Span<T> Advance(int requestedSize)
		{
			EnsureCapacity(requestedSize);

			var span = array.AsSpan(index, requestedSize);

			index += requestedSize;

			return span;
		}

		/// <inheritdoc cref="ImmutableArrayBuilder{T}.Add"/>
		public void Add(T value)
		{
			EnsureCapacity(1);

			array[index++] = value;
		}

		/// <inheritdoc cref="ImmutableArrayBuilder{T}.AddRange"/>
		public void AddRange(ReadOnlySpan<T> items)
		{
			EnsureCapacity(items.Length);

			items.CopyTo(array.AsSpan(index));

			index += items.Length;
		}

		/// <inheritdoc cref="ImmutableArrayBuilder{T}.Insert"/>
		public void Insert(int index, T item)
		{
			if (index < 0 || index > this.index)
			{
				ImmutableArrayBuilder.ThrowArgumentOutOfRangeExceptionForIndex();
			}

			EnsureCapacity(1);

			if (index < this.index)
			{
				Array.Copy(array, index, array, index + 1, this.index - index);
			}

			array[index] = item;
			this.index++;
		}

		/// <inheritdoc cref="ImmutableArrayBuilder{T}.Clear"/>
		public void Clear()
		{
			if (typeof(T) != typeof(byte) &&
			    typeof(T) != typeof(char) &&
			    typeof(T) != typeof(int))
			{
				array.AsSpan(0, index).Clear();
			}

			index = 0;
		}

		/// <summary>
		/// Ensures that <see cref="array"/> has enough free space to contain a given number of new items.
		/// </summary>
		/// <param name="requestedSize">The minimum number of items to ensure space for in <see cref="array"/>.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureCapacity(int requestedSize)
		{
			if (requestedSize > array.Length - index)
			{
				ResizeBuffer(requestedSize);
			}
		}

		/// <summary>
		/// Resizes <see cref="array"/> to ensure it can fit the specified number of new items.
		/// </summary>
		/// <param name="sizeHint">The minimum number of items to ensure space for in <see cref="array"/>.</param>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void ResizeBuffer(int sizeHint)
		{
			var minimumSize = index + sizeHint;
			var requestedSize = Math.Max(array.Length * 2, minimumSize);

			var newArray = new T[requestedSize];

			Array.Copy(array, newArray, index);

			array = newArray;
		}

		/// <inheritdoc/>
		int IList<T>.IndexOf(T item)
		{
			return Array.IndexOf(array, item, 0, index);
		}

		/// <inheritdoc/>
		void IList<T>.RemoveAt(int index)
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		bool ICollection<T>.Contains(T item)
		{
			return Array.IndexOf(array, item, 0, index) >= 0;
		}

		/// <inheritdoc/>
		void ICollection<T>.CopyTo(T[] array, int arrayIndex)
		{
			Array.Copy(this.array, 0, array, arrayIndex, index);
		}

		/// <inheritdoc/>
		bool ICollection<T>.Remove(T item)
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			T?[] array = this.array;
			
			var length = index;

			for (var i = 0; i < length; i++)
			{
				yield return array[i]!;
			}
		}

		/// <inheritdoc/>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<T>) this).GetEnumerator();
		}
	}
}

/// <summary>
/// Private helpers for the <see cref="ImmutableArrayBuilder{T}"/> type.
/// </summary>
file static class ImmutableArrayBuilder
{
	/// <summary>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> for <c>"index"</c>.
	/// </summary>
	public static void ThrowArgumentOutOfRangeExceptionForIndex()
	{
		throw new ArgumentOutOfRangeException("index");
	}
}