using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.Substring(0, length) simplifies to s[..length].</summary>
[InheritsTests]
public class StringSubstringFromZeroTest() : BaseTest<Func<string, int, string>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((s, length) => s.Substring(0, length));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((s, length) => s[..length]),
		Create((_, _) => "hel", [ "hello", 3 ]),
	];
}