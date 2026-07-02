namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Guard: when the loop body reads a method parameter (state from outside the matched
///   declaration+loop+return window), the char[]-loop pattern must NOT be rewritten — a `static`
///   string.Create lambda cannot capture it.
/// </summary>
[InheritsTests]
public class CharArrayLoopOuterCaptureNotRewrittenTest : BaseTest<Func<string, char, string>>
{
	public override string TestMethod => GetString((input, replacement) =>
	{
		var result = input.ToCharArray();

		for (var i = 0; i < result.Length; i++)
		{
			if (Char.IsWhiteSpace(result[i]))
			{
				result[i] = replacement;
			}
		}

		return new string(result);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => "hello_world", [ "hello world", '_' ])
	];
}