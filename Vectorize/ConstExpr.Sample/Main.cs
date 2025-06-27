using BenchmarkDotNet.Running;
using ConstExpr.SourceGenerator.Sample;
using ConstExpr.SourceGenerator.Sample.Tests;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

var range = Test.Range(10);

Console.WriteLine(Test.IsOdd(1, 2, 3, 4, 5));
Console.WriteLine(Test.Average(1f, 2f, 3f, 4f, 5f));
Console.WriteLine(Test.StdDev(1f, 2f, 3f, 4f, 5f));

Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8));
Console.WriteLine(Test.StringBytes("Hello, World!", Encoding.UTF8).Length);
Console.WriteLine(Test.Base64Encode("Hello, World!"));
Console.WriteLine(await Test.Waiting());
// Console.WriteLine(String.Join(", ", range.BinarySearch(11, Comparer<int>.Default)));
// Console.WriteLine(String.Join(", ", Test.Split("Hello, World!", ',')));
Console.WriteLine(String.Join(", ", Test.Fibonacci(20)));
Console.WriteLine(Test.RgbToHsl(150, 100, 50));

Console.WriteLine(CommonPrefixLength([0, 0, 0, 1, 2, 2, 3, 3, 4, 4]));

// Console.WriteLine(range.BinarySearch(2));

BenchmarkRunner.Run<ReplaceTest>();



static int CommonPrefixLength(ReadOnlySpan<int> other)
{
	if (other.IsEmpty)
	{
		return 0;
	}

	ReadOnlySpan<int> thisData = [0, 0, 0, 1, 2, 2, 3, 3, 4, 4];

	var position = 0;

	// Use Vector<T> for generic vectorization
	if (Vector.IsHardwareAccelerated)
	{
		var indexes = Vector<int>.Indices;
		var lengthVector = Vector.MinNative(Vector.Create(other.Length), Vector.Create(10));
		var countVector = Vector.Create(Vector<int>.Count);

		while (true)
		{
			var thisVec = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(thisData), (nuint)position);
			var otherVec = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(other), (nuint)position);

			var equalMask = Vector.Equals(thisVec, otherVec) & Vector.LessThan(indexes, lengthVector);

			if (equalMask != Vector<int>.AllBitsSet)
			{
				return position + Vector<int>.Count switch
				{
					4 => BitOperations.TrailingZeroCount(~Vector128.ExtractMostSignificantBits(equalMask.AsVector128())),
					8 => BitOperations.TrailingZeroCount(~Vector256.ExtractMostSignificantBits(equalMask.AsVector256())),
					16 => BitOperations.TrailingZeroCount(~Vector512.ExtractMostSignificantBits(equalMask.AsVector512())),
					_ => ExtractMostSignificantBitsFallback(equalMask),
				};
			}

			position += Vector<int>.Count;
			indexes += countVector;
		}
	}

	return thisData.CommonPrefixLength(other);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static int ExtractMostSignificantBitsFallback(Vector<int> mask)
	{
		// For larger vectors, fall back to element-wise checking
		// This is slower but more reliable for arbitrary vector sizes
		for (var i = 0; i < Vector<int>.Count; i++)
		{
			if (mask[i] == 0)
			{
				return i;
			}
		}

		return Vector<int>.Count;
	}
}