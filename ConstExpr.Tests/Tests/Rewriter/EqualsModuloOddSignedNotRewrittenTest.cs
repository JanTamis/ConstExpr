namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x % 2 == 1 must NOT fold to T.IsOddInteger(x) for a signed type without a proof that x is
///   non-negative — -3 % 2 == -1 in C#, so -3 % 2 == 1 is false even though -3 is odd.
/// </summary>
[InheritsTests]
public class EqualsModuloOddSignedNotRewrittenTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x % 2 == 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}