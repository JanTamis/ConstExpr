namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for SequenceEqual() optimization - verify same sequence returns true
/// </summary>
[InheritsTests]
public class LinqSequenceEqualOptimizationTests : BaseTest<Func<int[], bool>>
{
	public override string TestMethod => GetString(x =>
	{
		// SequenceEqual(same) => true
		var a = x.SequenceEqual(x);

		var b = Enumerable.Empty<int>().SequenceEqual(x);

		var c = x.SequenceEqual(Enumerable.Empty<int>());

		return a && b && c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Length <= 0),
		Create(_ => false, [ new[] { 1, 2, 3 } ]),
		Create(_ => true, [ System.Array.Empty<int>() ])
	];
}