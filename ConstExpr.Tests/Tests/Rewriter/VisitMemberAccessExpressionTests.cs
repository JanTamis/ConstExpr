namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitMemberAccessExpression - field/property evaluation
/// </summary>
[InheritsTests]
public class VisitMemberAccessExpressionTests : BaseTest
{
	public override string TestMethod => """
		(int, int, string, bool) TestMethod(string s, bool useEmpty)
		{
			var target = useEmpty ? string.Empty : s;
			var len = target.Length;
			var helloLen = "hello".Length;
			var empty = string.Empty;
			var isEmpty = target == string.Empty;
			return (len, helloLen, empty, isEmpty);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var target = useEmpty ? "" : s;
		var len = target.Length;
		var isEmpty = target == "";
		
		return (len, 5, "", isEmpty);
		""", Unknown, Unknown),
		Create("return (5, 5, \"\", false);", "hello", false),
		Create("return (0, 5, \"\", true);", "ignored", true),
		Create("return (3, 5, \"\", false);", "cat", false),
		Create("return (0, 5, \"\", true);", "", true)
	];
}
