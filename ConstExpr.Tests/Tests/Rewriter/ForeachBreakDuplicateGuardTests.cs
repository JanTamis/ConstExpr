namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   When the unrolled collection has duplicate values (the two 'l's in "hello"), the repeated,
///   loop-invariant break guard is provably never taken a second time and is dropped, while the
///   surviving increments collapse into a single compound assignment (index += 2).
/// </summary>
[InheritsTests]
public class ForeachBreakDuplicateGuardTests : BaseTest<Func<char, int>>
{
	public override string TestMethod => GetString(target =>
	{
		var index = 0;

		foreach (var c in "hello")
		{
			if (c == target)
			{
				break;
			}

			index++;
		}

		return index;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Runtime target: only one 'l' guard survives, and the two index++ fold to index += 2.
		Create(target =>
		{
			var index = 0;

			if (target == 'h')
				goto __unroll_break_0;

			index++;

			if (target == 'e')
				goto __unroll_break_0;

			index++;

			if (target == 'l')
				goto __unroll_break_0;

			index += 2;

			if (target == 'o')
				goto __unroll_break_0;

			index++;

			__unroll_break_0:
			return index;
		}),
		// The first 'l' still wins (index 2), and a miss still counts the whole string.
		Create("return 2;", 'l'),
		Create("return 4;", 'o'),
		Create("return 5;", 'x')
	];
}