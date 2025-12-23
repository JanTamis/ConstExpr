using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsPalindromeNumberTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 121),
		Create("return false;", 123)
	];
}