namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitArgumentList - visit list
/// </summary>
[InheritsTests]
public class VisitArgumentTests : BaseTest
{
	public override string TestMethod => """
		string TestMethod(string a, string b, string c)
		{
			return string.Concat(a, b, c);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return \"abc\";", "a", "b", "c"),
		Create("return String.Concat(\"a\", b, \"c\");", "a", Unknown, "c"),
		Create("return String.Concat(a, \"b\", c);", Unknown, "b", Unknown),
		Create("return string.Concat(a, \"ab\");", Unknown, "a", "b"),
		Create("return string.Concat(a, null, \"c\");", Unknown, null, "c"),
		Create("return \"\";", "", "", ""),
	];
}