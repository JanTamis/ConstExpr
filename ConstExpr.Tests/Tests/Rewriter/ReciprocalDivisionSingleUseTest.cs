namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A denominator declared as a Max reduction but divided into only once has nothing to share —
///   converting it to <c>* ReciprocalEstimate(max)</c> would just trade one division for one
///   approximation with no repeated call left for CSE to hoist, so it's left as a plain division.
///   <c>Math.Max</c> still retargets to <c>Double.MaxNative</c> independently of this.
/// </summary>
[InheritsTests]
public class ReciprocalDivisionSingleUseTest : BaseTestWithRandomValues<Func<double, double, double, double>>
{
	public override string TestMethod => GetString((x, y, z) =>
	{
		var max = System.Math.Max(System.Math.Max(x, y), z);

		return x / max;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, z) => x / Double.MaxNative(Double.MaxNative(x, y), z))
	];
}