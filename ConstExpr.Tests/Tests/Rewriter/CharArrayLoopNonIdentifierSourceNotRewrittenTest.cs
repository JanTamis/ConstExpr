namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Guard: when the <c>ToCharArray()</c> receiver is not a bare identifier (here, a method call),
///   the char[]-loop pattern must NOT be rewritten to <c>string.Create</c> — the receiver would
///   otherwise need to be evaluated twice (once for its length, once as the state argument).
/// </summary>
[InheritsTests]
public class CharArrayLoopNonIdentifierSourceNotRewrittenTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(input =>
	{
		var result = input.ToUpperInvariant().ToCharArray();

		for (var i = 0; i < result.Length; i++)
		{
			if (Char.IsWhiteSpace(result[i]))
			{
				result[i] = '_';
			}
		}

		return new string(result);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => "HELLO_WORLD", [ "hello world" ])
	];
}