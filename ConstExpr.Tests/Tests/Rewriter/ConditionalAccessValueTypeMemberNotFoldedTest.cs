namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Regression test for the value-type-return exclusion in the x?.Foo() fold: even though s is
///   provably non-null, s?.Length must NOT fold to s.Length — that would change the expression's
///   static type from int? to int (Length returns a value type).
/// </summary>
[InheritsTests]
public class ConditionalAccessValueTypeMemberNotFoldedTest : BaseTestWithRandomValues<Func<string, int?>>
{
	public override string TestMethod => GetString(s => s?.Length);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}