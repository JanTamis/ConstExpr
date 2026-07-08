namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A foreach over a constant string whose continue is guarded by a runtime value is unrolled into
///   a flat sequence of guarded gotos, each jumping to its own per-iteration label — a continue only
///   skips the rest of ITS OWN iteration, unlike break which exits the whole loop and so can share a
///   single trailing label (see <see cref="ForeachBreakUnrollBasicTests" />).
/// </summary>
[InheritsTests]
public class ForeachContinueUnrollBasicTests : BaseTest<Func<char, int>>
{
	public override string TestMethod => GetString(skip =>
	{
		var index = 0;

		foreach (var c in "abc")
		{
			if (c == skip)
			{
				continue;
			}

			index++;
		}

		return index;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Runtime skip: the loop unrolls, every continue becomes a goto to its own label.
		Create(skip =>
		{
			var index = 0;

			if (skip == 'a')
				goto __unroll_continue_0;

			index++;

			__unroll_continue_0:

			if (skip == 'b')
				goto __unroll_continue_1;

			index++;

			__unroll_continue_1:

			if (skip == 'c')
				goto __unroll_continue_2;

			index++;

			__unroll_continue_2:
			return index;
		}),
		// Constant skip still folds to a plain result.
		Create("return 2;", 'a'),
		Create("return 2;", 'b'),
		Create("return 2;", 'c'),
		Create("return 3;", 'z')
	];
}