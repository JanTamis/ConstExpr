namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SwapTest : BaseTest<Func<int, int, (int, int)>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		var temp = a;
		a = b;
		b = temp;

		return (a, b);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => (b, a)),
		CreateFolded(10, 20),
		CreateFolded(42, 0),
		CreateFolded(5, -5)
	];
}