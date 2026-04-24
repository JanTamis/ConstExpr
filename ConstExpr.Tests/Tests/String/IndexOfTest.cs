using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIndexOfTest() : BaseTest<Func<string, string, int>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((s, sub) => s.IndexOf(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return 1;", "hello", "ell"),
		Create("return -1;", "hello", "xyz"),
		Create("return 0;", "hello", "h"),
		Create("return 4;", "hello", "o"),
	];
}
