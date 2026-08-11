namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Reassigning the array would leave the hoisted reference pointing at the old one, so the pass
///   must skip an array that is written to.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationReassignedArrayTests : BaseTestWithRandomValues<Func<int[], int[], int>>
{
	public override string TestMethod => GetString((numbers, other) =>
	{
		if (other.Length > 0)
		{
			numbers = other;
		}

		return numbers[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}