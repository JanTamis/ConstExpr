namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ToArray() optimization - verify redundant materialization removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToArrayOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// ToArray().ToArray() => ToArray()
		var a = x.ToArray().Length;

		// ToList().ToArray() => ToArray()
		var b = x.ToList().ToArray().Length;

		// AsEnumerable().ToList().ToArray() => ToArray()
		var c = x.AsEnumerable().ToList().ToArray().Length;

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			var xLength = x.Length;

			return (xLength << 1) + xLength;
		}),
	];
}