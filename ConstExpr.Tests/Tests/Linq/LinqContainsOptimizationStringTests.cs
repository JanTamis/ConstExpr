namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Contains() with string values - verify optimization works with different types
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationStringTests : BaseTestWithRandomValues<Func<string[], int>>
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
		Create("return (Unsafe.BitCast<bool, byte>(Array.IndexOf(x, \"hello\") >= 0) << 1) + Unsafe.BitCast<bool, byte>(Array.IndexOf(x, \"world\") >= 0) + Unsafe.BitCast<bool, byte>(Array.Exists(x, v => String.Equals(v, \"HELLO\", StringComparison.CurrentCultureIgnoreCase)));", Unknown),
	];
}