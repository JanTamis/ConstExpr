namespace ConstExpr.Tests.Rewriter;

// Not a random-value test: the method writes into its (caller-owned) argument, and the folded-random
// harness can only express `return <value>;` - the write has to stay in the output.

/// <summary>
///   A tuple-deconstruction swap of two elements of a known parameter array writes into the caller's object,
///   so the swap must stay in the output (as one tuple assignment), and later reads fold against the swapped
///   contents instead of the original ones.
/// </summary>
[InheritsTests]
public class TupleAssignmentElementAccessParameterWriteTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(numbers =>
	{
		(numbers[0], numbers[1]) = (numbers[1], numbers[0]);

		return numbers[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);
			(numbersRef, Unsafe.Add(ref numbersRef, 1)) = (2, 1);

			return 2;
			""", new[] { 1, 2, 3 })
	];
}
