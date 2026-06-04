namespace ConstExpr.Tests.Rewriter;

/// <summary>
/// Tests for VisitObjectCreationExpression - constant constructor evaluation
/// </summary>
[InheritsTests]
public class VisitObjectCreationExpressionTests : BaseTest<Func<int, char[], (string, string)>>
{
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
		// When values are known and constant, they get inlined into the return statement
		Create((_, _) => ("aaaaa", "hello"), [ 5, new[] { 'h', 'e', 'l', 'l', 'o' } ]),
		Create((_, _) => ("", ""), [ 0, System.Array.Empty<char>() ]),
		Create((_, _) => ("aaa", "cat"), [ 3, new[] { 'c', 'a', 't' } ]),
		Create((_, _) => ("", "xyz"), [ 0, new[] { 'x', 'y', 'z' } ])
	];
}