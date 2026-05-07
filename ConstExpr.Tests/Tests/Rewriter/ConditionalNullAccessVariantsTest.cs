namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ConditionalNullAccessVariantsTest : BaseTest<Func<string, (int, int, int, int, int, int)>>
{
	public override string TestMethod => GetString(s =>
	{
		var a = s == null ? -1 : s.Length;
		var b = null == s ? -2 : s.Length;
		var c = s != null ? s.Length : -3;
		var d = null != s ? s.Length : -4;
		var e = s is null ? -5 : s.Length;
		var f = s is not null ? s.Length : -6;

		return (a, b, c, d, e, f);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (s?.Length ?? -1, s?.Length ?? -2, s?.Length ?? -3, s?.Length ?? -4, s?.Length ?? -5, s?.Length ?? -6);"),
		Create("return (3, 3, 3, 3, 3, 3);", "abc"),
		Create("return (-1, -2, -3, -4, -5, -6);", (object?)null)
	];
}
