namespace ConstExpr.Tests.String;

/// <summary>
///   Non-nullable parameter, UseNullableAnnotations on by default -> CanBeNull is proven false, so the
///   call site is the null-check-free IsWhiteSpaceFast(s.AsSpan()).
/// </summary>
[InheritsTests]
public class StringIsNullOrWhiteSpaceTest : BaseTestWithRandomValues<Func<string, bool>>
{
	public override string TestMethod => GetString(s => System.String.IsNullOrWhiteSpace(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return IsWhiteSpaceFast(s.AsSpan());")
	];
}