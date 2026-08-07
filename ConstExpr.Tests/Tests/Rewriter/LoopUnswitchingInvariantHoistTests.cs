using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A declaration inside one arm of the loop's if cannot be hoisted by LICM before unswitching:
///   LICM only looks at direct child statements of the loop's own block, and here that direct
///   child is the if, not the declaration. Once unswitching splits the loop per arm, the
///   declaration becomes a direct child of its own now-standalone loop and LICM can hoist it.
/// </summary>
[InheritsTests]
public class LoopUnswitchingInvariantHoistTests() : BaseTest<Func<int, int, int, bool, int>>(
	optimizations: OptimizationFlags.LoopUnswitching | OptimizationFlags.LoopInvariantCodeMotion)
{
	public override string TestMethod => GetString((n, x, y, flag) =>
	{
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			if (flag)
			{
				var prod = x * y;
				sum += prod;
				sum += prod;
			}
			else
			{
				sum -= i;
			}
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((n, x, y, flag) =>
		{
			var sum = 0;

			if (flag)
			{
				var prod = x * y;

				for (var i = 0; i < n; i++)
				{
					sum += prod;
					sum += prod;
				}
			}
			else
			{
				for (var i = 0; i < n; i++)
				{
					sum -= i;
				}
			}

			return sum;
		}, [ Unknown, Unknown, Unknown, Unknown ])
	];
}