using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Negative counterpart to <see cref="WhileToDoWhileConversionTests" />: same shape, but with the
///   early-exit guard removed, so <c>n</c> could be <c>&lt;= 0</c> and <c>i &lt;= n</c> is no longer
///   provably true on the first check (<c>n == 0</c> must run the loop zero times). The <c>while</c>
///   must stay a <c>while</c> — locks in that the pass does not over-fire.
/// </summary>
[InheritsTests]
public class WhileToDoWhileConversionNotProvenTest() : BaseTestWithRandomValues<Func<int, int>>(optimizations: OptimizationFlags.WhileToDoWhileConversion)
{
	protected override int MaxRandomMagnitudeBits => 5;

	public override string TestMethod => GetString(n =>
	{
		var i = 1;
		var count = 0;

		while (i <= n)
		{
			count++;
			i++;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}