using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Loop Fusion only fuses directly adjacent loops: a statement between them (which observes the
///   first loop's completed result) must block fusion.
/// </summary>
[InheritsTests]
public class LoopFusionStatementBetweenTests() : BaseTestWithRandomValues<Func<int, int>>(optimizations: OptimizationFlags.LoopFusion)
{

	protected override int MaxRandomMagnitudeBits => 5;
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