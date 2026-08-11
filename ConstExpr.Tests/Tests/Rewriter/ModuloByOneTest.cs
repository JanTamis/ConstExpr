namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for modulo optimizer strategies.</summary>
[InheritsTests]
public class ModuloByOneTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x % 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
	];
}