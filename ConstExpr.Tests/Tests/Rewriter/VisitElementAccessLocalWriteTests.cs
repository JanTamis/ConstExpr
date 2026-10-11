namespace ConstExpr.Tests.Rewriter;

/// <summary>Control: a write into a LOCAL array is invisible to the caller, so it still folds away entirely.</summary>
[InheritsTests]
public class VisitElementAccessLocalWriteTests : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var a = new[] { 1, 2, 3 };
		a[0] = n;

		return a[0] + a[1];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 11;", 9)
	];
}