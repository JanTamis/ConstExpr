namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SwapTest : BaseTestWithRandomValues<Func<int, int, (int, int)>>
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
	];
}