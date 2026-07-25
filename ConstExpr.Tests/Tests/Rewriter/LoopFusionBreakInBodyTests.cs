using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Loop Fusion must NOT fire when a body contains a control-flow escape: a <c>break</c> in the
///   first loop ends only that loop, but in a fused loop it would also cut the second body short.
/// </summary>
[InheritsTests]
public class LoopFusionBreakInBodyTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.LoopFusion)
{
	public override string TestMethod => GetString((n, limit) =>
	{
		var sum = 0;
		var prod = 0;

		for (var i = 0; i < n; i++)
		{
			if (i == limit)
			{
				break;
			}

			sum += i;
		}

		for (var i = 0; i < n; i++)
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