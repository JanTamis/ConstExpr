namespace ConstExpr.Tests.Rewriter;

/// <summary>
/// Tests for VisitArgumentList - visit list
/// </summary>
[InheritsTests]
public class VisitArgumentTests : BaseTest<Func<string, string, string, string>>
{
	public override string TestMethod => GetString((a, b, c) => System.String.Concat(a, b, c));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return String.Concat(a, b, c);"),
		Create("return \"abc\";", "a", "b", "c"),
		Create("return String.Concat(\"a\", b, \"c\");", "a", Unknown, "c"),
		Create("return String.Concat(a, \"b\", c);", Unknown, "b", Unknown),
		Create("return string.Concat(a, \"ab\");", Unknown, "a", "b"),
		Create("return string.Concat(a, null, \"c\");", Unknown, null, "c"),
		Create("return \"\";", "", "", "")
	];
}