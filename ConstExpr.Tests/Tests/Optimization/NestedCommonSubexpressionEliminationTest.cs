using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class NestedCommonSubexpressionEliminationTest() : BaseTest<Func<int, int, int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((x, y) =>
	{
		var result = 0;

		if (x > 0)
		{
			var a = x * x * x + 1;
			var b = x * x * x + 1;
			result = a * b;
		}
		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var result = 0;
			if (x > 0)
			{
				var sum = x * x * x + 1;
				result = sum * sum;
			}

			return result;
			"""),
		Create("return 4;", 1, 5)
	];
}