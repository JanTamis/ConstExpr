using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringEndsWithCharTest() : BaseTest<Func<string, char, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((s, c) => s.EndsWith(c));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, c) => c == 'o', [ "hello", Unknown ]),
		Create((_, _) => false, [ "", Unknown ]),
	];
}