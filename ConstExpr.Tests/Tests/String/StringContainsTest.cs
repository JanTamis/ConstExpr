using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringContainsTest() : BaseTest<Func<string, string, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((s, sub) => s.Contains(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Contains_57jrtQ(s, sub);"),
		Create((_, _) => true, [ "hello", "ell" ]),
		Create((_, _) => false, [ "hello", "world" ]),
		Create((_, _) => true, [ "abc", "" ]),
		Create((_, _) => false, [ "", "x" ]),
	];
}