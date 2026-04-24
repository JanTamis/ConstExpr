using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIsNullOrWhiteSpaceTest() : BaseTest<Func<string, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => string.IsNullOrWhiteSpace(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return true;", ""),
		Create("return true;", "   "),
		Create("return false;", "hello"),
		Create("return false;", " x "),
	];
}
