using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringEndsWithCharTest() : BaseTest<Func<string, char, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((s, c) => s.EndsWith(c));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return 'o' == c;", "hello", Unknown),
		Create("return false;", "", Unknown),
	];
}
