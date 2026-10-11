namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A static call that mutates a local array argument in place (<c>Array.Sort(local)</c>) must run against the
///   tracked array itself, not a copy rebuilt from its initializer, so later reads fold to the sorted contents.
/// </summary>
[InheritsTests]
public class VisitInvocationLocalArgumentMutationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(data =>
	{
		var local = new[] { 3, 1, 2 };
		System.Array.Sort(local);

		return local[0] + data.Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			System.Array.Sort(new[] { 3, 1, 2 });

			return 2;
			""", new[] { 1 })
	];
}