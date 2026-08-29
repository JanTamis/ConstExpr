namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Two-parameter accumulator recursion: <c>return Power(b, e - 1) * b;</c>. Only the
///   parameter carried by the recursive call's arguments is reassigned each iteration.
/// </summary>
[InheritsTests]
public class AccumulatorRecursionPowerTests : BaseTest<Func<long, int, long>>
{
	public override string TestMethod => """
		long TestMethod(long b, int e)
		{
			if (e <= 0)
			{
				return 1;
			}

			return TestMethod(b, e - 1) * b;
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var _tre_acc = 1L;

			while (true)
			{
				if (e <= 0)
				{
					return _tre_acc;
				}

				_tre_acc *= b;
				e -= 1;
			}
			""")
	];
}