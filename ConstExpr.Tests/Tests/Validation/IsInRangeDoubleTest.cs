namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeDoubleTest : BaseTestWithRandomValues<Func<double, double, double, bool>>
{
	public override string TestMethod => GetString((value, min, max) => value >= min && value <= max);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create("return FastAbs<double, ulong>(value - 5.5) <= 4.5;", Unknown, 1D, 10D),
		Create((_, _, _) => false, [ Unknown, 10D, 1D ]),
		Create((_, _, _) => false, [ Unknown, -1D, -10D ]),
		Create("return FastAbs<double, ulong>(value + 5.5) <= 4.5;", Unknown, -10D, -1D),
	];
}