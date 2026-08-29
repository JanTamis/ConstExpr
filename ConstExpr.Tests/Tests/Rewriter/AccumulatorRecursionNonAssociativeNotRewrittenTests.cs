namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A non-associative accumulator (subtraction) must be left completely alone — the
///   recursion stays as written.
/// </summary>
[InheritsTests]
public class AccumulatorRecursionNonAssociativeNotRewrittenTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => """
		int TestMethod(int n)
		{
			if (n <= 0)
			{
				return 0;
			}

			return TestMethod(n - 1) - n;
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return n <= 0 ? 0 : TestMethod(n - 1) - n;")
	];
}