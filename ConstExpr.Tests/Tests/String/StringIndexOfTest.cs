using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIndexOfTest() : BaseTest<Func<string, string, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((s, sub) => s.IndexOf(sub));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => 1, [ "hello", "ell" ]),
		Create((_, _) => -1, [ "hello", "xyz" ]),
		Create((_, _) => 0, [ "hello", "h" ]),
		Create((_, _) => 4, [ "hello", "o" ]),
	];
}