namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeTest : BaseTest<Func<int, int, int, bool>>
{
	public override string TestMethod => GetString((value, min, max) => value >= min && value <= max);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create("return (uint)(value - 1) <= 9U;", Unknown, 1, 10),
		Create((_, _, _) => false, [ Unknown, 10, 1 ]),
		Create((_, _, _) => false, [ Unknown, -1, -10 ]),
		Create("return (uint)(value + 10) <= 9U;", Unknown, -10, -1),
		CreateFolded(15, 1, 10),
		CreateFolded(1, 1, 10)
	];
}