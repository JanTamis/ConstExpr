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

/// <summary>
///   Loop Fusion only fuses directly adjacent loops: a statement between them (which observes the
///   first loop's completed result) must block fusion.
/// </summary>
[InheritsTests]
public class LoopFusionStatementBetweenTests() : BaseTest<Func<int, int>>(optimizations: OptimizationFlags.LoopFusion)
{
	public override string TestMethod => GetString(n =>
	{
		var sum = 0;
		var prod = 0;

		for (var i = 0; i < n; i++)
		{
			sum += i;
		}

		sum += n;

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