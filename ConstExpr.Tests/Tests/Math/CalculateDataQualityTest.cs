using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class CalculateDataQualityTest() : BaseTest<Func<double[], double>>(FastMathFlags.FastMath, LinqOptimisationMode.Optimize)
{
	public override string TestMethod => GetString(values =>
	{
		if (values.Length == 0)
		{
			return 0.0;
		}

		var nonNullCount = values.Count(v => !double.IsNaN(v) && !double.IsInfinity(v));

		return (double) nonNullCount / values.Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return values.Length == 0 ? 0D : (double)values.Count(Double.IsFinite) / values.Length;"),
	];
}