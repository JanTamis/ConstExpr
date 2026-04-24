using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringContainsTest() : BaseTest<Func<string, string, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((s, sub) => s.Contains(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return true;", "hello", "ell"),
		Create("return false;", "hello", "world"),
		Create("return true;", "abc", ""),
		Create("return false;", "", "x"),
	];
}
