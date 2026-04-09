using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPalindromeTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		var original = System.Math.Abs(n);
		var reversed = 0;
		var temp = original;

		while (temp > 0)
		{
			reversed = reversed * 10 + temp % 10;
			temp /= 10;
		}

		return original == reversed;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var original = AbsFast(n);
			var reversed = 0;
			var temp = original;
			
			while (temp > 0)
			{
				reversed = reversed * 10 + temp % 10;
				temp /= 10;
			}
			
			return original == reversed;
			""", Unknown),
		Create("return true;", 121),
		Create("return false;", 123)
	];
}