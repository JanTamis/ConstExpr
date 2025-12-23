namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitMemberAccessExpression - field/property evaluation
/// </summary>
[InheritsTests]
public class VisitMemberAccessExpressionTests : BaseTest<Func<string, bool, (int, int, string, bool)>>
{
	public override string TestMethod => GetString((s, useEmpty) =>
	{
		var target = useEmpty ? System.String.Empty : s;
		var len = target.Length;
		var helloLen = "hello".Length;
		var empty = System.String.Empty;
		var isEmpty = target == System.String.Empty;

		return (len, helloLen, empty, isEmpty);
	});

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