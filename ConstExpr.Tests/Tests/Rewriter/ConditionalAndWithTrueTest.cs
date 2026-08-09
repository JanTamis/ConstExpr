namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for conditional AND/OR optimizer strategies.</summary>
[InheritsTests]
public class ConditionalAndWithTrueTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b && true);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(b => b),
		CreateFolded(true),
		CreateFolded(false)
	];
}