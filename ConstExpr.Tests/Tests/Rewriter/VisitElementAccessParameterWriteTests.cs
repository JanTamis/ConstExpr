namespace ConstExpr.Tests.Rewriter;

// Not random-value tests: each method writes into its (caller-owned) argument, and the folded-random
// harness can only express `return <value>;` - the write has to stay in the output.

/// <summary>
///   An element write into a known parameter array is the caller's object (possibly aliased there), so it
///   must stay in the output. Later reads still fold against the written value.
/// </summary>
[InheritsTests]
public class VisitElementAccessParameterWriteTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(arr =>
	{
		arr[0] = 9;

		return arr[0] + arr[1];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);
			arrRef = 9;

			return 11;
			""", new[] { 1, 2 })
	];
}