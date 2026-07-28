namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Test with larger set of values
/// </summary>
[InheritsTests]
public class PatternBitmaskLargeTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(n =>
	{
		return n is 0 or 10 or 20 or 30 or 40 or 50 or 60 or 70 or 80;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => (uint) n <= 80U && n % 10 == 0),
		Create(_ => true, [ 0 ]),
		Create(_ => true, [ 10 ]),
		Create(_ => true, [ 20 ]),
		Create(_ => true, [ 30 ]),
		Create(_ => true, [ 40 ]),
		Create(_ => true, [ 50 ]),
		Create(_ => true, [ 60 ]),
		Create(_ => false, [ 5 ]),
		Create(_ => false, [ 25 ])
	];
}