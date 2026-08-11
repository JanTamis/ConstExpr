namespace ConstExpr.Tests.Math;

/// <summary>Truncate(-x) → -(Truncate(x)): moves negation outside.</summary>
[InheritsTests]
public class MathTruncateNegationTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Truncate(-x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => -Double.Truncate(x))
	];
}