namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringLengthTest : BaseTestWithRandomValues<Func<string?, int>>
{
	public override string TestMethod => GetString(s =>
	{
		if (s is null)
		{
			return -1;
		}

		return s.Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s?.Length ?? -1),
	];
}