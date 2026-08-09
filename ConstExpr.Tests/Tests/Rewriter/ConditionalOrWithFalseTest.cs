namespace ConstExpr.Tests.Rewriter;

/// <summary>b || false = b.</summary>
[InheritsTests]
public class ConditionalOrWithFalseTest : BaseTest<Func<bool, bool>>
{
	public override string TestMethod => GetString(b => b || false);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(b => b),
		CreateFolded(true),
		CreateFolded(false)
	];
}