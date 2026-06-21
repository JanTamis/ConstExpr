namespace ConstExpr.Tests.Rewriter;

/// <summary>b || true = true.</summary>
[InheritsTests]
public class ConditionalOrWithTrueTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || true);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => true),
		Create(_ => true, [ true ]),
		Create(_ => true, [ false ])
	];
}