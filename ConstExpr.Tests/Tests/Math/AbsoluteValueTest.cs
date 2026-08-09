namespace ConstExpr.Tests.Math;

[InheritsTests]
public class AbsoluteValueTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0)
		{
			return -n;
		}

		return n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAbs(n);"),
		CreateFolded(-42),
		CreateFolded(10),
		CreateFolded(0)
	];
}