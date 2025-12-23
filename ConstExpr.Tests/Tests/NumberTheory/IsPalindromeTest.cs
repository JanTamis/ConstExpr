using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPalindromeTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var original = Int32.Abs(n);
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