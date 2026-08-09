namespace ConstExpr.Tests.String;

/// <summary>
///   Positive control paired with StringIsNullOrEmptyFlagOffTest: default flags, non-nullable s, so
///   the call must fold to s.Length == 0. StringIsNullOrEmptyTest uses string? and so never exercises
///   the folding direction itself.
/// </summary>
[InheritsTests]
public class StringIsNullOrEmptyNonNullTest : BaseTest<Func<string, bool>>
{
	public override string TestMethod => GetString(s => System.String.IsNullOrEmpty(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s.Length == 0),
		CreateFolded(System.String.Empty),
		CreateFolded("hello")
	];
}