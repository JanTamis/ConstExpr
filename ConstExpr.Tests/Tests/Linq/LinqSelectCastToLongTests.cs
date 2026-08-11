namespace ConstExpr.Tests.Linq;

[InheritsTests]
public class LinqSelectCastToLongTests : BaseTestWithRandomValues<Func<IEnumerable<int>, long>>
{
	public override string TestMethod => GetString(x =>
	{
		// Select(y => (long)y) → Cast<long>()
		return x.Select(y => (long) y).Sum();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Sum_vjIGww(x);"),
	];
}