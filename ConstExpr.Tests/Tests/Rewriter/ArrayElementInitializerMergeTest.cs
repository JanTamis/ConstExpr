using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A <c>new T[N]</c> declaration followed by N sequential constant-index assignments
///   (<c>result[0] = ...; result[1] = ...;</c>) is merged into a single array initializer
///   (<c>new T[] { ..., ... }</c>).
/// </summary>
[InheritsTests]
public class ArrayElementInitializerMergeTest : BaseTest<Func<int[], int, int[]>>
{
	public override string TestMethod => GetString((numbers, positions) =>
	{
		var result = new int[numbers.Length];

		for (var i = 0; i < result.Length; i++)
		{
			result[i] = numbers[(positions + i) % 6];
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numbers, positions) =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
			var result = new int[numbers.Length];
			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);

			for (var i = 0; i < result.Length; i++)
			{
				Unsafe.Add(ref resultRef, i) = Unsafe.Add(ref numbersRef, (positions + i) % 6);
			}

			return result;
		}),
		Create((numbers, positions) =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);

			return
			[
				Unsafe.Add(ref numbersRef, positions % 6),
				Unsafe.Add(ref numbersRef, (positions + 1) % 6),
				Unsafe.Add(ref numbersRef, (positions + 2) % 6),
				Unsafe.Add(ref numbersRef, (positions + 3) % 6),
				Unsafe.Add(ref numbersRef, (positions + 4) % 6),
				Unsafe.Add(ref numbersRef, (positions + 5) % 6)
			];
		}, [ new[] { 1, 2, 3, 4, 5, 6 }, Unknown ])
	];
}