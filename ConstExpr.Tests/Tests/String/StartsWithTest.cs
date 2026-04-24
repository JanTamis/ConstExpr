using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StartsWithTest() : BaseTest<Func<string, string, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((s, prefix) => s.StartsWith(prefix));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return true;", "hello", "hel"),
		Create("return false;", "world", "foo"),
		Create("return true;", "", "")
	];
}