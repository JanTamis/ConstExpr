namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x + 2 != 0 must NOT fold to T.IsOddInteger(x) — the odd/even modulo-detection strategies only
///   apply when the left operand of the comparison is itself a modulo expression (x % 2), not an
///   arbitrary binary expression whose right-hand operand happens to be the constant 2.
/// </summary>
[InheritsTests]
public class NotEqualsModuloOddNonModuloLeftNotRewrittenTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x + 2 != 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}