namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class MultiplyByPowerOfTwoTest : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((n, power) => n << power);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => 40, [ 10, 2 ]),
		Create((_, _) => 0, [ 0, 5 ]),
		Create((_, _) => 128, [ 4, 5 ])
	];
}