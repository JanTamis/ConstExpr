namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for multiply optimizer strategies.</summary>
[InheritsTests]
public class MultiplyByZeroTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x * 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 0;"),
		Create("return 0;", 99),
		Create("return 0;", -5),
	];
}