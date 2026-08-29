namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A body mixing a bare tail call with an accumulator call is not a shape either path
///   handles — it must be left unchanged.
/// </summary>
[InheritsTests]
public class AccumulatorRecursionMixedWithTailCallNotRewrittenTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => """
		int TestMethod(int n)
		{
			if (n <= 0)
			{
				return 0;
			}

			if (n == 100)
			{
				return TestMethod(n - 1);
			}

			return n + TestMethod(n - 1);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return n <= 0 ? 0 : n == 100 ? TestMethod(n - 1) : n + TestMethod(n - 1);")
	];
}