namespace ConstExpr.Tests.Linq;

[InheritsTests]
public class LinqSelectCastToLongTests : BaseTest<Func<IEnumerable<int>, long>>
{
	public override string TestMethod => GetString(x =>
	{
		// Select(y => (long)y) → Cast<long>()
		return x.Select(y => (long) y).Sum();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateFolded(new[] { 1, 2, 3 }),
		CreateFolded(Enumerable.Empty<int>()),
		CreateFolded(new[] { 42 })
	];
}