namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A foreach whose body mixes a runtime-guarded continue with a runtime-guarded break still
///   unrolls: the break becomes a goto to one shared trailing label (see
///   <see cref="ForeachBreakUnrollBasicTests" />), while the continue becomes a goto to its own
///   per-iteration label (see <see cref="ForeachContinueUnrollBasicTests" />) — the two rewrites
///   are independent and compose without conflict.
/// </summary>
[InheritsTests]
public class ForeachBreakContinueUnrollBasicTests : BaseTestWithRandomValues<Func<char, char, int>>
{
	public override string TestMethod => GetString((skip, stop) =>
	{
		var index = 0;

		foreach (var c in "abc")
		{
			if (c == skip)
			{
				continue;
			}

			if (c == stop)
			{
				break;
			}

			index++;
		}

		return index;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Runtime skip and stop: the loop unrolls, each continue becomes a goto to its own
		// per-iteration label, and the break becomes a goto to one shared trailing label.
		Create((skip, stop) =>
		{
			var index = 0;

			if (skip == 'a')
				goto __unroll_continue_0;

			if (stop == 'a')
				goto __unroll_break_0;

			index++;

			__unroll_continue_0:

			if (skip == 'b')
				goto __unroll_continue_1;

			if (stop == 'b')
				goto __unroll_break_0;

			index++;

			__unroll_continue_1:

			if (skip == 'c')
				goto __unroll_continue_2;

			if (stop == 'c')
				goto __unroll_break_0;

			index++;

			__unroll_continue_2:
			__unroll_break_0:
			return index;
		}),
	];
}