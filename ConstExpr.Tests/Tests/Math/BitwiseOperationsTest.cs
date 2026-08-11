namespace ConstExpr.Tests.Math;

[InheritsTests]
public class BitwiseOperationsTest : BaseTestWithRandomValues<Func<int, int, int>>
{
	public override string TestMethod => GetString((a, b) => a & b | a ^ b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}