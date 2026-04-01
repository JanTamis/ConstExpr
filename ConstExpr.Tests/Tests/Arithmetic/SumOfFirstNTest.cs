using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfFirstNTest() : BaseTest<Func<int, int>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(n => n * (n + 1) / 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown),
		Create("return 55;", 10),
		Create("return 0;", 0),
		Create("return 5050;", 100)
	];
}