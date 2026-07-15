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
		Create(_ => 6L, [ new[] { 1, 2, 3 } ]),
		Create(_ => 0L, [ Enumerable.Empty<int>() ]),
		Create(_ => 42L, [ new[] { 42 } ])
	];
}