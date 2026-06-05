namespace ConstExpr.Tests.String;

[InheritsTests]
public class IndexOfAnySearchValuesTest() : BaseTest<Func<string, int>>()
{
	public override string TestMethod => GetString(s => s.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' }));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Constant char set + runtime instance => cached SearchValues<char> lookup over a span.
		Create("return s.AsSpan().IndexOfAny(SearchValues_2fghYg);", Unknown)
	];
}