namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for a large constant char/byte set: collection.Contains(x) => cached SearchValues&lt;T&gt; probe.
///   Small sets stay on the is-pattern tier; sets larger than 8 elements use SearchValues.
/// </summary>
[InheritsTests]
public class LinqContainsSearchValuesTests : BaseTest<Func<char[], char, bool>>
{
	public override string TestMethod => GetString((values, x) =>
	{
		return values.Contains(x);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return SearchValues_nlEluQ.Contains(x);", new[] { 'a', 'e', 'i', 'o', 'u', 'y', 'w', 'q', 'z' }, Unknown)
	];
}

/// <summary>
///   Byte variant: a large constant byte set uses SearchValues&lt;byte&gt; (built via new byte[] { ... }
///   to disambiguate the SearchValues.Create overloads).
/// </summary>
[InheritsTests]
public class LinqContainsSearchValuesByteTests : BaseTest<Func<byte[], byte, bool>>
{
	public override string TestMethod => GetString((values, x) =>
	{
		return values.Contains(x);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return SearchValues_gJYlMw.Contains(x);", new byte[] { 1, 2, 3, 5, 8, 13, 21, 34, 55 }, Unknown)
	];
}