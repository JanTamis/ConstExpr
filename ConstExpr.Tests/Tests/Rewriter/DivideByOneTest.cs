namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for divide optimizer strategies.</summary>
[InheritsTests]
public class DivideByOneTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x / 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x),
		CreateFolded(9),
		CreateFolded(-4)
	];
}