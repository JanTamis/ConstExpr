namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfSquaresTest() : BaseTestWithRandomValues<Func<int, int>>(maxUnrollIterations: Int32.MaxValue)
{
	// The loop runs n times, so n must stay well under maxUnrollIterations for every generated
	// case to fully unroll to a literal - otherwise the expected body (computed by just invoking
	// TestMethod) would assume folding that the rewriter correctly refuses to do for large n.
	protected override int MaxRandomMagnitudeBits => 6;

	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return 0;
		}

		var total = 0;

		for (var i = 1; i <= n; i++)
		{
			total += i * i;
		}

		return total;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}