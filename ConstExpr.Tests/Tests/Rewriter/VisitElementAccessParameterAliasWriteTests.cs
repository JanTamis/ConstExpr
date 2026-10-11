namespace ConstExpr.Tests.Rewriter;

// Not random-value tests: the method writes into its (caller-owned) argument, and the folded-random
// harness can only express `return <value>;` - the write has to stay in the output.

/// <summary>
///   A write through a single-use alias of a parameter array (<c>var a = arr; a[0] = 9;</c>) is a write to
///   the caller's array. The alias is inlined away, so the write is emitted through <c>arr</c> and later
///   reads of <c>arr</c> fold against the written value.
/// </summary>
[InheritsTests]
public class VisitElementAccessParameterAliasWriteTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(arr =>
	{
		var a = arr;

		a[0] = 9;

		return arr[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);
			arrRef = 9;

			return 9;
			""", new[] { 1, 2 })
	];
}
