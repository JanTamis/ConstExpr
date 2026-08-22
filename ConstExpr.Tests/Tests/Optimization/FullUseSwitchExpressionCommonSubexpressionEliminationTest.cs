using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class FullUseSwitchExpressionCommonSubexpressionEliminationTest() : BaseTestWithRandomValues<Func<int, int, int, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
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
				result = x * y + 2;
				break;
			default:
				result = x * y + 3;
				break;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, i) =>
		{
			var prod = x * y;

			return i switch
			{
				0 => prod + 1,
				1 => prod + 2,
				_ => prod + 3
			};
		})
	];
}