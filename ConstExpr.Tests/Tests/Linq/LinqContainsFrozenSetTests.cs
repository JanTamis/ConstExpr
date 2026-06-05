namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for a large constant string set: collection.Contains(x) => cached FrozenSet&lt;string&gt; probe.
///   Vector-supported element types keep their SIMD scan; char/byte use SearchValues; strings use FrozenSet.
/// </summary>
[InheritsTests]
public class LinqContainsFrozenSetTests : BaseTest<Func<string[], string, bool>>
{
	public override string TestMethod => GetString((values, x) =>
	{
		return values.Contains(x);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FrozenSet_XieT4Q.Contains(x);",
			new[] { "if", "else", "while", "for", "do", "switch", "case", "break", "return" }, Unknown)
	];
}