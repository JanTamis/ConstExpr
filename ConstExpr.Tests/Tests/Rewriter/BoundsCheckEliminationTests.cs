using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Bounds-check elimination on an array parameter: one <c>ref</c> local is hoisted to the top of
///   the body, indexing by <c>0</c> becomes that local, and every other index becomes an
///   <c>Unsafe.Add</c> offset. <c>.Length</c> is left alone.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(numbers =>
	{
		var sum = numbers[0];

		for (var i = 1; i < numbers.Length; i++)
		{
			sum += numbers[i];
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(numbers =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
			var sum = numbersRef;

			for (var i = 1; i < numbers.Length; i++)
			{
				sum += Unsafe.Add(ref numbersRef, i);
			}

			return sum;
		}),

		// Known input folds through the interpreter before the pass ever sees an array.
		CreateFolded(new[] { 1, 2, 3, 4, 5 })
	];
}