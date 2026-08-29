namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A base-case <c>return</c> nested inside a construct the rewriter does not descend into
///   (here a <c>for</c> loop) must abort the whole transform — otherwise it would escape with
///   the bare base value while <c>treAcc</c> still holds pending factors.
/// </summary>
[InheritsTests]
public class AccumulatorRecursionReturnInLoopNotRewrittenTests : BaseTest<Func<int, long>>
{
	// The `for` has a runtime bound, so no earlier pass folds it away — the `return 1;` nested
	// inside it survives to the TRE pass, which must then refuse the whole transform.
	public override string TestMethod => """
		long TestMethod(int n)
		{
			for (var i = 2; i <= n; i++)
			{
				if (i == n)
				{
					return 1;
				}
			}

			return TestMethod(n - 1) * n;
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			for (var i = 2; i <= n; i++)
			{
				if (i == n)
					return 1L;
			}

			return TestMethod(n - 1) * n;
			""")
	];
}