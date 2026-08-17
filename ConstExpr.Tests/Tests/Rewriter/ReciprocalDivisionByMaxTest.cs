namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A denominator declared as <c>Max(Max(x,y),z)</c> and divided into 3 times (all unconditional,
///   in the same tuple) is converted to <c>* ReciprocalEstimate(max)</c> at each occurrence, then the
///   repeated <c>ReciprocalEstimate(max)</c> call is hoisted by ordinary CSE into <c>invMax</c> — the
///   same identity <c>RGBToCMYKTest</c> exercises, but here the source already declares the
///   denominator as a Max reduction, so no scale-factor distribution is needed to reach it.
/// </summary>
[InheritsTests]
public class ReciprocalDivisionByMaxTest : BaseTestWithRandomValues<Func<double, double, double, (double, double, double)>>
{
	public override string TestMethod => GetString((x, y, z) =>
	{
		var max = System.Math.Max(System.Math.Max(x, y), z);

		return (x / max, y / max, z / max);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, z) =>
		{
			var max = Double.MaxNative(Double.MaxNative(x, y), z);
			var invMax = Double.ReciprocalEstimate(max);

			return (x * invMax, y * invMax, z * invMax);
		})
	];
}