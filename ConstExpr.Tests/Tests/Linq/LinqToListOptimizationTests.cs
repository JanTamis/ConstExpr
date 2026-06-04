using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for ToList() optimization - verify redundant materialization removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToListOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// ToList().ToList() => ToList()
		var a = x.ToList().ToList().Count;

		// ToArray().ToList() => ToList()
		var b = x.ToArray().ToList().Count;

		// AsEnumerable().ToArray().ToList() => ToList()
		var c = x.AsEnumerable().ToArray().ToList().Count;

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x.Length << 1) + x.Length),
		Create(_ => 9, [ new[] { 1, 2, 3 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ]),
	];
}