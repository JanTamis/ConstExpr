namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DivideByPowerOfTwoTest : BaseTestWithRandomValues<Func<int, int, int>>
{
	public override string TestMethod => GetString((n, power) => n >> power);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}