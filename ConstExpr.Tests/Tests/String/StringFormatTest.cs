using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>string.Format with a constant format string is rewritten to an interpolated string.</summary>
[InheritsTests]
public class StringFormatTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(name => string.Format("Hello {0}", name));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return \"Hello World\";", "World"),
	];
}