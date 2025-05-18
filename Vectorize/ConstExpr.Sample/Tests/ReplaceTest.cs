using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;

namespace ConstExpr.SourceGenerator.Sample.Tests;

public class ReplaceTest
{
	public static ReadOnlySpan<int> ICustomCollection_32690199_Data => [ 0, 1, 4, 5, 5, 5, 6, 7, 8, 8, 8, 10, 11, 13, 13, 15, 16, 18, 18, 19 ];
	
	[Benchmark]
	[Arguments(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 5, 10)]
	public void CopyTo(Span<int> destination, int oldValue, int newValue)
	{
		var oldValueVector = Vector128.Create(oldValue);
		var newValueVector = Vector128.Create(newValue);

		var vec0 = Vector128.Create(0, 1, 2, 2);
		var vec1 = Vector128.Create(6, 6, 12, 12);
		var vec2 = Vector128.Create(12, 13, 14, 14);
		var vec3 = Vector128.Create(16, 16, 17, 17);
		var vec4 = Vector128.Create(17, 18, 19, 19);

		var vecEquals0 = Vector128.Equals(vec0, oldValueVector);
		var vecEquals1 = Vector128.Equals(vec1, oldValueVector);
		var vecEquals2 = Vector128.Equals(vec2, oldValueVector);
		var vecEquals3 = Vector128.Equals(vec3, oldValueVector);
		var vecEquals4 = Vector128.Equals(vec4, oldValueVector);

		var result0 = Vector128.ConditionalSelect(vecEquals0, newValueVector, vec0);
		var result1 = Vector128.ConditionalSelect(vecEquals1, newValueVector, vec1);
		var result2 = Vector128.ConditionalSelect(vecEquals2, newValueVector, vec2);
		var result3 = Vector128.ConditionalSelect(vecEquals3, newValueVector, vec3);
		var result4 = Vector128.ConditionalSelect(vecEquals4, newValueVector, vec4);

		result0.StoreUnsafe(ref MemoryMarshal.GetReference(destination), 0);
		result1.StoreUnsafe(ref MemoryMarshal.GetReference(destination), 4);
		result2.StoreUnsafe(ref MemoryMarshal.GetReference(destination), 8);
		result3.StoreUnsafe(ref MemoryMarshal.GetReference(destination), 12);
		result4.StoreUnsafe(ref MemoryMarshal.GetReference(destination), 16);
	}

	[Benchmark]
	[Arguments(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 5, 10)]
	public void CopyTo2(Span<int> destination, int oldValue, int newValue)
	{
		var oldValueVector256 = Vector256.Create(oldValue);
		var newValueVector256 = Vector256.Create(newValue);

		var vec0 = Vector256.Create(0, 0, 0, 2, 4, 4, 4, 5);
		var vec2 = Vector256.Create(6, 6, 8, 8, 9, 9, 11, 13);
		var vec4 = Vector128.Create(14, 15, 17, 18);

		Vector256.ConditionalSelect(Vector256.Equals(vec0, oldValueVector256), newValueVector256, vec0).StoreUnsafe(ref MemoryMarshal.GetReference(destination), 0);
		Vector256.ConditionalSelect(Vector256.Equals(vec2, oldValueVector256), newValueVector256, vec2).StoreUnsafe(ref MemoryMarshal.GetReference(destination), 8);
		Vector128.ConditionalSelect(Vector128.Equals(vec4, oldValueVector256.GetLower()), newValueVector256.GetLower(), vec4).StoreUnsafe(ref MemoryMarshal.GetReference(destination), 16);
	}
}