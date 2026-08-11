namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CharToLowerEqualsOptimizerTest : BaseTestWithRandomValues<Func<char, char, bool>>
{
	public override string TestMethod => GetString((left, right) =>
	{
		return Char.ToLower(left) == Char.ToLower(right);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((left, right) => left.Equals(right, StringComparison.CurrentCultureIgnoreCase)),
	];
}