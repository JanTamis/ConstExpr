namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for bitwise OR De Morgan: ~a | ~b => ~(a &amp; b).</summary>
[InheritsTests]
public class OrDeMorganTest : BaseTestWithRandomValues<Func<int, int, int>>
{
	public override string TestMethod => GetString((a, b) => ~a | ~b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => ~(a & b))
	];
}