using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringToCharArrayTest() : BaseTest<Func<string, char[]>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s => s.ToCharArray());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => [ 'h', 'i' ], [ "hi" ]),
		Create(_ => [ 'a', 'b', 'c' ], [ "abc" ]),
		Create(_ => [ ], [ "" ]),
	];
}