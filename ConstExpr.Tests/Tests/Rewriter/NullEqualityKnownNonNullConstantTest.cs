namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A const local is a known, non-null literal — Visit replaces the identifier with the literal
///   value before the new fold's IsProvablyNonNull check ever runs (which only matches an
///   IdentifierNameSyntax). This is a regression guard proving the new early-placed check in
///   VisitBinaryExpression doesn't interfere with the pre-existing literal-value fold path, not a
///   test of the new check's own logic.
/// </summary>
[InheritsTests]
public class NullEqualityKnownNonNullConstantTest : BaseTest<Func<bool>>
{
	public override string TestMethod => GetString(() =>
	{
		const string s = "x";
		return s == null;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(() => false)
	];
}