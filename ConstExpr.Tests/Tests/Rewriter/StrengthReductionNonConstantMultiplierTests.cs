namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Negative test for strength reduction: the multiplier is a runtime variable, not an integer
///   literal, so no accumulator step can be computed. The pass must leave the loop unchanged.
/// </summary>
[InheritsTests]
public class StrengthReductionNonConstantMultiplierTests : BaseTestWithRandomValues<Func<int, int, int>>
{
	// n is the loop bound - keep it small enough to stay under the default maxUnrollIterations (32)
	// so RunRandomTests's known-value cases can still fully interpret the loop (the strength-reduction
	// pass itself won't fire either way, since m isn't a literal - that's this class's own point).
	protected override int MaxRandomMagnitudeBits => 5;

	public override string TestMethod => GetString((n, m) =>
	{
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			sum += i * m;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}