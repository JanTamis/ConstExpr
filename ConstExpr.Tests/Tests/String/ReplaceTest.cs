using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.Replace("x","x") with same old/new is a no-op.</summary>
[InheritsTests]
public class StringReplaceNoOpTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => s.Replace("a", "a"));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return s;"),
	];
}

/// <summary>When replacement differs, no optimisation is applied.</summary>
[InheritsTests]
public class StringReplaceWithDifferentArgsTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => s.Replace("a", "b"));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return s.Replace('a', 'b');"),
		Create("return \"hello\";", "hello"),
		Create("return \"bbnbnb\";", "banana"),
	];
}
