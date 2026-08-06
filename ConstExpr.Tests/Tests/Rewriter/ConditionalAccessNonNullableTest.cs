namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ConditionalAccessNonNullableTest : BaseTest<Func<string, string?>>
{
	// ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract — being non-nullable is the point.
	public override string TestMethod => GetString(s => s?.Trim());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s.Trim())
	];
}