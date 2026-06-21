using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ToArray() optimization - verify redundant materialization removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToArrayOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
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
		Create(x => (x.Length << 1) + x.Length),
		Create(_ => 9, [ new[] { 1, 2, 3 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ])
	];
}