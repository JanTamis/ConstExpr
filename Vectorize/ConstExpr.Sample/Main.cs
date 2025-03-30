using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using ConstExpr.SourceGenerator.Sample;

Console.WriteLine(Test.IsOdd(1f, 2f, 3f, 4f, 5f));
Console.WriteLine(Test.Average(1f, 2f, 3f, 4f, 5f));
Console.WriteLine(Test.StdDev(1f, 2f, 3f, 4f, 5f));

Console.WriteLine(Test.StringLength("Hello, World!", Encoding.UTF8));
Console.WriteLine(Test.StringBytes("Hello, World!", Encoding.UTF8).Length);
Console.WriteLine(Test.Base64Encode("Hello, World!"));
Console.WriteLine(await Test.Waiting());
Console.WriteLine(String.Join(", ", Test.Range(4)));
Console.WriteLine(String.Join(", ", Test.Split("Hello, World!", ',')));
Console.WriteLine(String.Join(", ", Test.Fibonacci(20)));
Console.WriteLine(Test.RgbToHsl(150, 100, 50));

Console.WriteLine(CommonPrefixLength([1, 3, 1, 1, 1]));

int CommonPrefixLength(ReadOnlySpan<int> other)
{
	if (Vector128.IsHardwareAccelerated && Vector128<int>.IsSupported)
	{
		var countVec = Vector128.Min(Vector128.Create(other.Length), Vector128.Create(4));
		var otherVec = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(other));

		var sequence = Vector128.Create(0, 1, 2, 3);
		var result = Vector128.Create(1, 3, 1, 1);

		var mask = Vector128.LessThan(sequence, countVec) & Vector128.Equals(otherVec, result);
		var matchBits = Vector128.ExtractMostSignificantBits(mask);

		return BitOperations.PopCount(matchBits);
	}
	
	if (other.Length == 0 || other[0] != 1) return 0;
	if (other.Length == 1 || other[1] != 3) return 1;
	if (other.Length == 2 || other[2] != 1) return 2;
	if (other.Length == 3 || other[3] != 1) return 3;
	return 4;
}