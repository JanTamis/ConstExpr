namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsEvenTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0)
		{
			n = -n;
		}

		return (n & 1) == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Int32.IsEvenInteger(FastAbs(n));"),
		Create(_ => true, [ 4 ]),
		Create(_ => false, [ 5 ])
	];
}