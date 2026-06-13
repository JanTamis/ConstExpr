using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPalindromeTest() : BaseTest<Func<int, bool>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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
				reversed = reversed * 10 + temp - (((int)(temp * 1717986919L >> 32) >> 2) - (temp >> 31)) * 10;
				temp = ((int)(temp * 1717986919L >> 32) >> 2) - (temp >> 31);
			}

			return original == reversed;
			"""),
		Create(_ => true, [ 121 ]),
		Create(_ => false, [ 123 ])
	];
}