namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsPalindromeNumberTest : BaseTestWithRandomValues<Func<int, bool>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0)
		{
			return false;
		}

		var original = n;
		var reversed = 0;

		while (n > 0)
		{
			reversed = reversed * 10 + n % 10;
			n /= 10;
		}

		return original == reversed;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}