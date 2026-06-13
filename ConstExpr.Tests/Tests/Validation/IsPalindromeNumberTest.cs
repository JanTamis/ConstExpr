using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsPalindromeNumberTest() : BaseTest<Func<int, bool>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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
		Create(n =>
		{
			if (n < 0)
				return false;

			var original = n;
			var reversed = 0;

			while (n > 0)
			{
				reversed = reversed * 10 + n - (((int) (n * 1717986919L >> 32) >> 2) - (n >> 31)) * 10;
				n = ((int) (n * 1717986919L >> 32) >> 2) - (n >> 31);
			}

			return original == reversed;
		}),
		Create(_ => true, [ 121 ]),
		Create(_ => false, [ 123 ])
	];
}