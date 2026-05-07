using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class BetterNamesTest() : BaseTest<Func<int, int, int>>(FastMathFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((x, y) =>
	{
		var s1 = x * x + y * y;
		var s2 = x * x + y * y;
		return s1 + s2;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var sum = x * x + y * y;
			return sum + sum;
			"""),
		Create("return 50;", 3, 4)
	];
}