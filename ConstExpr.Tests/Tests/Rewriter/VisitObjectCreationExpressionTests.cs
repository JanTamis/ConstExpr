namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitObjectCreationExpression - constant constructor evaluation
/// </summary>
[InheritsTests]
public class VisitObjectCreationExpressionTests : BaseTestWithRandomValues<Func<int, char[], (string, string)>>
{
	protected override int MaxRandomMagnitudeBits => 8;

	public override string TestMethod => GetString((amount, chars) =>
	{
		var s1 = new string('a', amount);
		var s2 = new string(chars);

		return (s1, s2);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// When values are unknown, keep the original code unchanged
		Create((amount, chars) => (new string('a', amount), new string(chars))),
	];
}