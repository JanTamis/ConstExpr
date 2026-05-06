using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringToCharArrayTest() : BaseTest<Func<string, char[]>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => s.ToCharArray());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return ['h', 'i'];", "hi"),
		Create("return ['a', 'b', 'c'];", "abc"),
		Create("return [];", ""),
	];
}

