namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Regression test: when the receiver of a conditional-access expression is known to be exactly
///   a null literal (not just Unknown), VisitConditionalAccessExpression must fold x?.Foo() to plain
///   null. TryGetLiteralValue matches a null literal too (its Token.Value is just null), so the
///   null-literal check must run before the general TryGetLiteralValue branch — otherwise this folds
///   to the invalid `null.Trim()` instead.
/// </summary>
[InheritsTests]
public class ConditionalAccessKnownNullTest : BaseTestWithRandomValues<Func<string?, string?>>
{
	public override string TestMethod => GetString(s => s?.Trim());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateFolded((object?) null)
	];
}