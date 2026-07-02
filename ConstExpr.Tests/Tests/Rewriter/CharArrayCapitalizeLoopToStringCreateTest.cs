namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A <c>ToCharArray()</c> declaration followed by a canonical <c>for</c> loop that mutates the
///   array in place, followed by <c>new string(...)</c>, is rewritten to <c>string.Create</c>.
/// </summary>
[InheritsTests]
public class CharArrayCapitalizeLoopToStringCreateTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(input =>
	{
		var result = input.ToCharArray();
		var capitalizeNext = true;

		for (var i = 0; i < result.Length; i++)
		{
			var c = result[i];

			if (Char.IsWhiteSpace(c))
			{
				capitalizeNext = true;
			}
			else if (capitalizeNext)
			{
				result[i] = Char.ToUpper(c);
				capitalizeNext = false;
			}
		}

		return new string(result);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => "Hello World", [ "hello world" ]),
		Create(_ => "", [ System.String.Empty ]),
		Create(_ => "Already Capitalized", [ "Already Capitalized" ])
	];
}