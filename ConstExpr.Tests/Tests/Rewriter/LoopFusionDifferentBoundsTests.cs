using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Loop Fusion must NOT fire when the iteration spaces differ (<c>i &lt; n</c> vs <c>i &lt; m</c>).
/// </summary>
[InheritsTests]
public class LoopFusionDifferentBoundsTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.LoopFusion)
{
	public override string TestMethod => GetString((n, m) =>
	{
		var sum = 0;
		var prod = 0;

		for (var i = 0; i < n; i++)
		{
			sum += i;
		}

		for (var i = 0; i < m; i++)
		{
			prod += i;
		}

		return sum + prod;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}