using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   The second safety check for <see cref="ValueRangeEarlyExitGuardTest" />, and the harder one:
///   the guard and the write are not in the same block. The early exit narrows <c>n</c> to
///   <c>[.., 5]</c> for the statements after it, but the use sits inside a loop whose body assigns
///   to <c>n</c> — so on the second iteration the guard describes a value that is long gone, and
///   <c>n &lt; 50</c> is genuinely undecided.
///   <para>
///     <see cref="ValueRangeEarlyExitWriteTest" /> does not reach this: there the write precedes the
///     use in the same block, which the per-statement reset handles. Here the fact has to be dropped
///     one block level further out, by the same <c>killed</c> flag that already protects a
///     declaration's initializer.
///   </para>
///   <para>
///     The trip count has to be a second unknown parameter. With a constant bound the loop is
///     unrolled long before this pass runs, which lifts the use into the block the guard is in and
///     makes the fold both legal and uninteresting.
///   </para>
/// </summary>
[InheritsTests]
public class ValueRangeEarlyExitLoopWriteTest : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((n, count) =>
	{
		if (n > 5)
		{
			return -1;
		}

		var sum = 0;

		for (var i = 0; i < count; i++)
		{
			sum += n < 50 ? 1 : 0;
			n = 100;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// The guard inside the loop stays exactly where it was. Only the always-on bool-to-int
		// BitCast rewrite touches it, which is what makes this differ from `CreateDefault()`.
		Create((n, count) =>
		{
			if (n > 5)
			{
				return -1;
			}

			var sum = 0;

			for (var i = 0; i < count; i++)
			{
				// ReSharper disable once RedundantCast — the emitted body has it, so the match needs it.
				sum += (int) Unsafe.BitCast<bool, byte>(n < 50);
				n = 100;
			}

			return sum;
		}),

		// One pass counts, the second does not — folding the guard to true would answer 2.
		CreateFolded(1, 2),
		CreateFolded(40, 2)
	];
}