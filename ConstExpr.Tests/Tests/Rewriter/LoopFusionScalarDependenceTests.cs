using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Loop Fusion must NOT fire when the second body reads a scalar the first body writes: after
///   fusion, iteration k of the second body would see a partial <c>sum</c> instead of the final
///   one. The loops must be left unchanged.
/// </summary>
[InheritsTests]
public class LoopFusionScalarDependenceTests() : BaseTest<Func<int, int>>(optimizations: OptimizationFlags.LoopFusion)
{
	public override string TestMethod => GetString(n =>
	{
		var sum = 0;
		var scaled = 0;

		for (var i = 0; i < n; i++)
		{
			sum += i;
		}

		for (var i = 0; i < n; i++)
		{
			scaled += sum;
		}

		return scaled;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// null expected body => assert the optimized body is identical to the original.
		CreateDefault()
	];
}