namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPalindromeTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 121),
		Create("return false;", 123),
	];

	public override string TestMethod => """
		bool IsPalindrome(int n)
		{
			var original = Math.Abs(n);
			var reversed = 0;
			var temp = original;
			
			while (temp > 0)
			{
				reversed = reversed * 10 + temp % 10;
				temp /= 10;
			}
			
			return original == reversed;
		}
		""";
}

