using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>x / x DOES fold to 1 once a sibling comparison in the same condition proves x is non-zero.</summary>
[InheritsTests]
public class DivideIdempotencyProvenNonZeroTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x =>
	{
		if (x != 0)
		{
			return x / x;
		}

		return 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Unsafe.BitCast<bool, byte>(x != 0))
	];
}