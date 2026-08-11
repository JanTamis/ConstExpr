namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Both sides are null literals, not identifiers — the new fold's IsProvablyNonNull check requires
///   an IdentifierNameSyntax, so it correctly no-ops here and this must still fold to true via the
///   pre-existing literal-value path (hasLeftValue &amp;&amp; hasRightValue). Proves the new early-placed
///   check doesn't change the answer for the both-literal case.
/// </summary>
[InheritsTests]
public class NullEqualityBothNullLiteralTest : BaseTestWithRandomValues<Func<bool>>
{
	public override string TestMethod => GetString(() => null == null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(() => true)
	];
}