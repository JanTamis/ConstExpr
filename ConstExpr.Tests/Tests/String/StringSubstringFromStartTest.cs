using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.Substring(start) converts to s[start..] range syntax.</summary>
[InheritsTests]
public class StringSubstringFromStartTest() : BaseTest<Func<string, int, string>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString((s, start) => s.Substring(start));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((s, start) => s[start..]),
		Create((_, _) => "llo", [ "hello", 2 ])
	];
}