namespace ConstExpr.Tests.Rewriter;

// Not a random-value test (see TupleAssignmentElementAccessParameterWriteTests): the method mutates its
// (caller-owned) argument.

/// <summary>
///   The classic in-place reverse written with a tuple swap. Once the loop is evaluated at compile time the
///   emitted swap must use literal indices (the loop locals are pruned) and the method must hand back the
///   caller's array, not a collection literal of the pre-swap contents.
/// </summary>
[InheritsTests]
public class TupleAssignmentElementAccessSwapLoopTests : BaseTest<Func<int[], int[]>>
{
	public override string TestMethod => GetString(numbers =>
	{
		for (int left = 0, right = numbers.Length - 1; left < right; left++, right--)
			(numbers[left], numbers[right]) = (numbers[right], numbers[left]);

		return numbers;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
			(numbersRef, Unsafe.Add(ref numbersRef, 2)) = (3, 1);

			return numbers;
			""", new[] { 1, 2, 3 })
	];
}
