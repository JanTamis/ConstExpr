namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ConditionalNullAccessVariantsTest : BaseTest<Func<string, (int, int, int, int, int, int)>>
{
	public override string TestMethod => GetString(s =>
	{
		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract — being always false is the point.
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
		// UseNullableAnnotations now folds each null-check condition (s is non-nullable) to a constant
		// bool, so each ternary collapses straight to s.Length instead — which CSE then dedupes into
		// one shared local, since all six variables compute the identical expression.
		Create("""
			var sLength = s.Length;

			return (sLength, sLength, sLength, sLength, sLength, sLength);
			"""),
		CreateFolded("abc"),
		CreateFolded((object?) null)
	];
}