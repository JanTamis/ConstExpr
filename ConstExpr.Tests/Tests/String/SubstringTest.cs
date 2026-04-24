using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.Substring(start) converts to s[start..] range syntax.</summary>
[InheritsTests]
public class StringSubstringFromStartTest() : BaseTest<Func<string, int, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((s, start) => s.Substring(start));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return s[start..];"),
		Create("return \"llo\";", "hello", 2),
	];
}

/// <summary>s.Substring(0, length) simplifies to s[..length].</summary>
[InheritsTests]
public class StringSubstringFromZeroTest() : BaseTest<Func<string, int, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((s, length) => s.Substring(0, length));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return s[..length];"),
		Create("return \"hel\";", "hello", 3),
	];
}
