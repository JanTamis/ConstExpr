namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SignTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n > 0)
		{
			return 1;
		}

		if (n < 0)
		{
			return -1;
		}

		return 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => n > 0 ? 1 : n < 0 ? -1 : 0),
		CreateFolded(100),
		CreateFolded(-50),
		CreateFolded(0)
	];
}