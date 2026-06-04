using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeTest() : BaseTest<Func<int, int, int, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((value, min, max) => value >= min && value <= max);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return (uint)(value - 1) <= 9U;", Unknown, 1, 10),
		Create((_, _, _) => false, [ Unknown, 10, 1 ]),
		Create((_, _, _) => false, [ Unknown, -1, -10 ]),
		Create("return (uint)(value + 10) <= 9U;", Unknown, -10, -1),
		Create((_, _, _) => false, [ 15, 1, 10 ]),
		Create((_, _, _) => true, [ 1, 1, 10 ])
	];
}