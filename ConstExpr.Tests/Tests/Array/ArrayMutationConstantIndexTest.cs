namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayMutationConstantIndexTest : BaseTest<Func<int>>
{
	public override string TestMethod => GetString(() =>
	{
		var counts = new int[256];

		foreach (var c in "aba")
		{
			counts[c]++;
		}

		return counts['a'];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(() => 2)
	];
}