using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using ConstExpr.SourceGenerator.Sample;

var range = Test.Range(5);

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

Console.WriteLine(range.BinarySearch(2));

void Replace(Span<int> destination, int oldValue, int newValue)
{
	if (20U > destination.Length)
	{
		throw new ArgumentOutOfRangeException("destination", "The length of the span is less than 20");
	}
	
	if (Vector128.IsHardwareAccelerated)
	{
		var oldValueVector = Vector128.Create(oldValue);
		var newValueVector = Vector128.Create(newValue);
		
		Vector128.ConditionalSelect(Vector128.Equals(Vector128<int>.Zero, oldValueVector), newValueVector, Vector128<int>.Zero).StoreUnsafe(ref MemoryMarshal.GetReference(destination), 0);
		Vector128.ConditionalSelect(Vector128.Equals(Vector128<int>.Zero, oldValueVector), newValueVector, Vector128<int>.Zero).StoreUnsafe(ref MemoryMarshal.GetReference(destination), 4);
		Vector128.ConditionalSelect(Vector128.Equals(Vector128.Create(1, 1, 2, 2), oldValueVector), newValueVector, Vector128.Create(1, 1, 2, 2)).StoreUnsafe(ref MemoryMarshal.GetReference(destination), 8);
		Vector128.ConditionalSelect(Vector128.Equals(Vector128.Create(2, 3, 4, 4), oldValueVector), newValueVector, Vector128.Create(2, 3, 4, 4)).StoreUnsafe(ref MemoryMarshal.GetReference(destination), 12);
		Vector128.ConditionalSelect(Vector128.Equals(Vector128.CreateSequence(4, 0), oldValueVector), newValueVector, Vector128.CreateSequence(4, 0)).StoreUnsafe(ref MemoryMarshal.GetReference(destination), 16);
		
		return;
	}
	
	destination[0] = 0 == oldValue ? newValue : 0;
	destination[1] = 0 == oldValue ? newValue : 0;
	destination[2] = 0 == oldValue ? newValue : 0;
	destination[3] = 0 == oldValue ? newValue : 0;
	destination[4] = 0 == oldValue ? newValue : 0;
	destination[5] = 0 == oldValue ? newValue : 0;
	destination[6] = 0 == oldValue ? newValue : 0;
	destination[7] = 0 == oldValue ? newValue : 0;
	destination[8] = 1 == oldValue ? newValue : 1;
	destination[9] = 1 == oldValue ? newValue : 1;
	destination[10] = 2 == oldValue ? newValue : 2;
	destination[11] = 2 == oldValue ? newValue : 2;
	destination[12] = 2 == oldValue ? newValue : 2;
	destination[13] = 3 == oldValue ? newValue : 3;
	destination[14] = 4 == oldValue ? newValue : 4;
	destination[15] = 4 == oldValue ? newValue : 4;
	destination[16] = 4 == oldValue ? newValue : 4;
	destination[17] = 4 == oldValue ? newValue : 4;
	destination[18] = 4 == oldValue ? newValue : 4;
	destination[19] = 4 == oldValue ? newValue : 4;
}