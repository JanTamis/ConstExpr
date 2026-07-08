namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Several breaks in one loop body all jump to the same trailing label after unrolling. Two
///   guards per iteration with the same jump target collapse into a single "a || b" condition.
/// </summary>
[InheritsTests]
public class ForeachMultipleBreakUnrollTests : BaseTest<Func<char, char, int>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		var index = 0;

		foreach (var c in "abc")
		{
			if (c == a)
			{
				break;
			}

			if (c == b)
			{
				break;
			}

			index++;
		}

		return index;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Both breaks per iteration merge into one "a == x || b == x" guard.
		Create((a, b) =>
		{
			var index = 0;

			if (a == 'a' || b == 'a')
				goto __unroll_break_0;

			index++;

			if (a == 'b' || b == 'b')
				goto __unroll_break_0;

			index++;

			if (a == 'c' || b == 'c')
				goto __unroll_break_0;

			index++;

			__unroll_break_0:
			return index;
		}),
		// Constant args fold to the index of the first char matching either target.
		Create("return 0;", 'a', 'z'),
		Create("return 1;", 'z', 'b'),
		Create("return 3;", 'y', 'z')
	];
}