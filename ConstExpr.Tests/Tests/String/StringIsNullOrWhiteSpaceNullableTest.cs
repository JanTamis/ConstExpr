namespace ConstExpr.Tests.String;

/// <summary>
///   Nullable parameter -> CanBeNull stays true regardless of UseNullableAnnotations, so the call site
///   is the null-checking IsNullOrWhiteSpaceFast(s).
/// </summary>
[InheritsTests]
public class StringIsNullOrWhiteSpaceNullableTest : BaseTestWithRandomValues<Func<string?, bool>>
{
	public override string TestMethod => GetString(s => System.String.IsNullOrWhiteSpace(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return IsNullOrWhiteSpaceFast(s);")
	];
}