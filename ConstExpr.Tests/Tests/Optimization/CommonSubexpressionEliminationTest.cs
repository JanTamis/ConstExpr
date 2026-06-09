using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CommonSubexpressionEliminationTest() : BaseTest<Func<int, int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((x, y) =>
	{
		var a = x * y + 1;
		var b = x * y + 1;
		return a * b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) =>
		{
			var sum = x * y + 1;
			return sum * sum;
		}),
		Create((_, _) => 121, [ 2, 5 ])
	];
}