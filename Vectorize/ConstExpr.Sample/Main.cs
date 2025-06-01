using BenchmarkDotNet.Running;
using ConstExpr.SourceGenerator.Sample;
using ConstExpr.SourceGenerator.Sample.Tests;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

var range = Test.Range(10);

Console.WriteLine(Test.IsOdd(1f, 2f, 3f, 4f, 5f));
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

// Console.WriteLine(range.BinarySearch(2));

BenchmarkRunner.Run<ReplaceTest>();

static unsafe void Replace(Span<int> destination, int oldValue, int newValue)
{
	ArgumentOutOfRangeException.ThrowIfLessThan((uint)destination.Length, 10U);

	if (Vector256.IsHardwareAccelerated)
	{
		var pointer = (int*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(destination));
		var oldValueVector = Vector256.Create(oldValue);
		var newValueVector = Vector256.Create(newValue);

		var vec0 = Vector256.Create(0, 1, 2, 3, 3, 4, 4, 4);
		var indices = Vector256<int>.Indices;

		var elements = Vector256.ConditionalSelect(Vector256.Equals(vec0, oldValueVector), newValueVector, vec0);

		Avx2.MaskStore(pointer, Vector256.LessThan(Vector256.Create(destination.Length), indices), elements);

		return;
	}

	destination[0] = oldValue == 0 ? newValue : 0;
	destination[1] = oldValue == 1 ? newValue : 1;
	destination[2] = oldValue == 2 ? newValue : 2;
	destination[3] = oldValue == 3 ? newValue : 3;
	destination[4] = oldValue == 3 ? newValue : 3;
	destination[5] = oldValue == 4 ? newValue : 4;
	destination[6] = oldValue == 4 ? newValue : 4;
	destination[7] = oldValue == 4 ? newValue : 4;
	destination[8] = oldValue == 4 ? newValue : 4;
	destination[9] = oldValue == 4 ? newValue : 4;
}