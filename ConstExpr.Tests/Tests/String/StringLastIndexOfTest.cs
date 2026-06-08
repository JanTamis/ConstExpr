using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringLastIndexOfTest() : BaseTest<Func<string, string, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((s, sub) => s.LastIndexOf(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => 3, [ "hello", "l" ]),
		Create((_, _) => -1, [ "hello", "world" ]),
		Create((_, _) => 0, [ "hello", "h" ]),
		Create((_, _) => 4, [ "hello", "o" ]),
	];
}