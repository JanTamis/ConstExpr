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

/// <summary>string.Format with two placeholders is rewritten to an interpolated string.</summary>
[InheritsTests]
public class StringFormatTwoArgsTest() : BaseTest<Func<string, string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((first, last) => string.Format("{0} {1}", first, last));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return \"John Doe\";", "John", "Doe"),
	];
}

