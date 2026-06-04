using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StartsWithTest() : BaseTest<Func<string, string, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((s, prefix) => s.StartsWith(prefix));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => true, [ "hello", "hel" ]),
		Create((_, _) => false, [ "world", "foo" ]),
		Create((_, _) => true, [ "", "" ])
	];
}