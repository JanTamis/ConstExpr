using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class NonExhaustiveSwitchCommonSubexpressionEliminationTest() : BaseTestWithRandomValues<Func<int, int, int, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((x, y, i) =>
	{
		var result = 0;

		switch (i)
		{
			case 0:
				result = x * y + 1;
				break;
			case 1:
				result = x * y + 1;
				break;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, i) =>
		{
			var result = 0;

			switch (i)
			{
				case 0:
					result = x * y + 1;
					break;
				case 1:
					result = x * y + 1;
					break;
			}

			return result;
		})
	];
}