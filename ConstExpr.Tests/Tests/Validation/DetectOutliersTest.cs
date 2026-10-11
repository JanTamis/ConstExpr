namespace ConstExpr.Tests.Validation;

/// <summary>
///   Mirrors <c>DataValidationOperations.DetectOutliers</c> in the Sample: with a fully known input no element lies
///   more than two standard deviations from the mean, so the result must fold to an empty array.
/// </summary>
[InheritsTests]
public class DetectOutliersTest : BaseTestWithRandomValues<Func<double[], double, int[]>>
{
	public override string TestMethod => GetString((data, standardDeviations) =>
	{
		if (data.Length < 2)
		{
			return System.Array.Empty<int>();
		}

		var mean = data.Average();
		var variance = data.Average(x => System.Math.Pow(x - mean, 2));
		var stdDev = System.Math.Sqrt(variance);
		var threshold = stdDev * standardDeviations;

		var outliers = new List<int>();

		for (var i = 0; i < data.Length; i++)
		{
			if (System.Math.Abs(data[i] - mean) > threshold)
			{
				outliers.Add(i);
			}
		}

		return outliers.ToArray();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return [];", new[] { 1D, 2D, 3D, 4D, 5D }, 2D),
		// An unknown threshold leaves every comparison to runtime: no conditional Add may leak into a folded result.
		Create("""
			var threshold = standardDeviations * 1.4142135623730951;
			var outliers = new List<int>();
			var lt = threshold < 2D;

			if (lt)
				outliers.Add(0);

			var lt2 = threshold < 1D;

			if (lt2)
				outliers.Add(1);

			if (threshold < 0D)
				outliers.Add(2);

			if (lt2)
				outliers.Add(3);

			if (lt)
				outliers.Add(4);

			return outliers.ToArray();
			""", new[] { 1D, 2D, 3D, 4D, 5D }, Unknown)
	];
}