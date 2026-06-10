using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StartsWithTest() : BaseTest<Func<string, string, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((s, prefix) => s.StartsWith(prefix));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => true, [ "hello", "hel" ]),
		Create((_, _) => false, [ "world", "foo" ]),
		Create((_, _) => true, [ "", "" ])
	];
}