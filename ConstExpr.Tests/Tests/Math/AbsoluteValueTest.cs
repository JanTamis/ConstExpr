using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class AbsoluteValueTest() : BaseTest<Func<int, int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0)
		{
			return -n;
		}

		return n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return n < 0 ? -n : n;"),
		Create("return 42;", -42),
		Create("return 10;", 10),
		Create("return 0;", 0)
	];
}