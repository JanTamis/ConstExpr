namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for the accumulator shape of Tail-Recursion Elimination:
///   <c>return MethodName(…) * factor;</c> / <c>return factor + MethodName(…);</c> mixed
///   with base-case returns. The pending <c>*</c>/<c>+</c> operations are threaded through an
///   introduced <c>treAcc</c> local so the recursion becomes a <c>while (true)</c> loop.
///   Only Unknown-argument cases exercise the rewriter — with known arguments the partial
///   rewriter constant-folds the whole recursion first.
/// </summary>
[InheritsTests]
public class AccumulatorRecursionFactorialTests : BaseTest<Func<int, long>>
{
	/// <summary>
	///   Classic <c>return Factorial(n - 1) * n;</c> — the recursive call is the left operand
	///   of a multiply.
	/// </summary>
	public override string TestMethod => """
		long TestMethod(int n)
		{
			if (n <= 1)
			{
				return 1;
			}

			return TestMethod(n - 1) * n;
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var treAcc = 1L;

			while (true)
			{
				if (n <= 1)
				{
					return treAcc;
				}

				treAcc *= n;
				n -= 1;
			}
			""")
	];
}