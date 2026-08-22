using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class PartialUseSwitchExpressionCommonSubexpressionEliminationTest() : BaseTestWithRandomValues<Func<int, int, int, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((x, y, i) =>
	{
		int result;

		switch (i)
		{
			case 0:
				result = x * y + 1;
				break;
			case 1:
				result = x * y + 1;
				break;
			default:
				result = 0;
				break;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, i) =>
		{
			return i switch
			{
				0 => x * y + 1,
				1 => x * y + 1,
				_ => 0
			};
		})
	];
}