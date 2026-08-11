namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitMemberAccessExpression - field/property evaluation
/// </summary>
[InheritsTests]
public class VisitMemberAccessExpressionTests : BaseTestWithRandomValues<Func<string, bool, (int, int, string, bool)>>
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((s, useEmpty) => (useEmpty ? 0 : s.Length, 5, "", useEmpty || s == "")),
	];
}