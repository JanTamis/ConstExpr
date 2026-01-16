using System.Collections;

namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ projection removal - verify that unnecessary Select/Map operations are eliminated
/// </summary>
[InheritsTests]
public class LinqProjectionRemovalTests : BaseTest<Func<IEnumerable<double>, double[]>>
{
	public override string TestMethod => GetString(x =>
	{
		// Select identity function - should be removed
		var a = x.Select(v => v).First();

		// Select with type cast that's already correct
		var b = x.Select(v => (double)v).Sum();

		// Select with simple field access on constant objects
		var c = x.Select(v => v).Count();

		// Nested Select that can be flattened
		var d = x
			.Select(v => v * 2)
			.Select(v => v + 1)
			.First();

		// SelectMany on single-element array
		var e = new[] { x }.SelectMany(arr => arr).Count();

		return [a, b, c, d, e];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 16;", 5),
		Create("return 16;", 100),
	];
}

