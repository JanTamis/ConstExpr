using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Contains() with string values - verify optimization works with different types
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationStringTests() : BaseTest<Func<string[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Simple Contains with string
		var a = x.Contains("hello") ? 1 : 0;

		// Distinct().Contains() => Contains()
		var b = x.Distinct().Contains("world") ? 1 : 0;

		// Select(...).Contains() with string transformation
		var c = x.Select(v => v.ToUpper()).Contains("HELLO") ? 1 : 0;

		// Where(...).Contains()
		var d = x.Where(v => v.Length > 3).Contains("hello") ? 1 : 0;

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (Array.IndexOf(x, \"hello\") >= 0 ? 2 : 0) + (Array.IndexOf(x, \"world\") >= 0 ? 1 : 0) + (Array.Exists(x, v => String.Equals(v, \"HELLO\", StringComparison.CurrentCultureIgnoreCase)) ? 1 : 0);", Unknown),
		Create(_ => 4, [ new[] { "hello", "world", "foo" } ]),
		Create(_ => 0, [ System.Array.Empty<string>() ]),
		Create(_ => 1, [ new[] { "hi", "world", "test" } ]) // Only b matches ("world")
	];
}