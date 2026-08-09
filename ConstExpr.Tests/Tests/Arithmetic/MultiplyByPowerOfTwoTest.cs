namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class MultiplyByPowerOfTwoTest : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((n, power) => n << power);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(10, 2),
		CreateFolded(0, 5),
		CreateFolded(4, 5)
	];
}