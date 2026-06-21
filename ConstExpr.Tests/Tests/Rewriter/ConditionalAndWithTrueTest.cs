namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for conditional AND/OR optimizer strategies.</summary>
[InheritsTests]
public class ConditionalAndWithTrueTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b && true);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(b => b),
		Create(_ => true, [ true ]),
		Create(_ => false, [ false ])
	];
}