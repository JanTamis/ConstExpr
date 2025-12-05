using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsPalindromeNumberTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 121),
		Create("return false;", 123),
	];

	public override string TestMethod => """
		bool IsPalindromeNumber(int n)
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
		}
		""";
}

