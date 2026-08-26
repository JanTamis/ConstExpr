namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for bitwise AND De Morgan: ~a &amp; ~b => ~(a | b).</summary>
[InheritsTests]
public class AndDeMorganTest : BaseTestWithRandomValues<Func<int, int, int>>
{
	public override string TestMethod => GetString((a, b) => ~a & ~b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => ~(a | b))
	];
}