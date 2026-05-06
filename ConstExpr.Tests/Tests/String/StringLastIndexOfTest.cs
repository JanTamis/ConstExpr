using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringLastIndexOfTest() : BaseTest<Func<string, string, int>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((s, sub) => s.LastIndexOf(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return 3;", "hello", "l"),
		Create("return -1;", "hello", "world"),
		Create("return 0;", "hello", "h"),
		Create("return 4;", "hello", "o"),
	];
}

