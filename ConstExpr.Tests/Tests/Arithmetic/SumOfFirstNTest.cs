namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfFirstNTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n => n * (n + 1) / 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var prod = n * (n + 1);

			return prod + (prod >>> 31) >> 1;
		})
	];
}