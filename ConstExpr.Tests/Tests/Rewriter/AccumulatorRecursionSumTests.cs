namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Right-operand recursion with the <c>+</c> accumulator: <c>return n + Sum(n - 1);</c>.
/// </summary>
[InheritsTests]
public class AccumulatorRecursionSumTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => """
		int TestMethod(int n)
		{
			if (n <= 0)
			{
				return 0;
			}

			return n + TestMethod(n - 1);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var treAcc = 0;

			while (true)
			{
				if (n <= 0)
				{
					return treAcc;
				}

				treAcc += n;
				n -= 1;
			}
		})
	];
}