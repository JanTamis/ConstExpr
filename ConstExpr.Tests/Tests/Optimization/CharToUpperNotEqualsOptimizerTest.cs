namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CharToUpperNotEqualsOptimizerTest : BaseTest<Func<char, char, bool>>
{
	public override string TestMethod => GetString((left, right) =>
	{
		return Char.ToUpper(left) != Char.ToUpper(right);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((left, right) => !left.Equals(right, StringComparison.CurrentCultureIgnoreCase)),
		CreateFolded('a', 'A'),
		CreateFolded('a', 'B')
	];
}