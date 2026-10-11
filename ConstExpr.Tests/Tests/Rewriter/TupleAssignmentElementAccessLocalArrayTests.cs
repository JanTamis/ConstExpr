namespace ConstExpr.Tests.Rewriter;

// Not a random-value test (see TupleAssignmentElementAccessParameterWriteTests): the expected body is
// written out explicitly.

/// <summary>
///   A tuple-deconstruction swap of two elements of a local array folds into the tracked array, so a later
///   read sees the swapped value and the swap itself disappears from the output.
/// </summary>
[InheritsTests]
public class TupleAssignmentElementAccessLocalArrayTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var a = new[] { 1, 2, 3 };

		(a[0], a[1]) = (a[1], a[0]);

		return a[0] + n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 12;", 10)
	];
}
