namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A foreach over a constant string whose break is guarded by a runtime value is unrolled into a
///   flat sequence of guarded gotos plus a trailing label, instead of falling back to a real loop.
/// </summary>
[InheritsTests]
public class ForeachBreakUnrollBasicTests : BaseTest<Func<char, int>>
{
	public override string TestMethod => GetString(target =>
	{
		var index = 0;

		foreach (var c in "abc")
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
		// Runtime target: the loop unrolls, every break becomes a goto to one trailing label.
		Create(target =>
		{
			var index = 0;

			if (target == 'a')
				goto __unroll_break_0;

			index++;

			if (target == 'b')
				goto __unroll_break_0;

			index++;

			if (target == 'c')
				goto __unroll_break_0;

			index++;

			__unroll_break_0:
			return index;
		}),
		// Constant target still folds to a plain result.
		Create("return 1;", 'b'),
		Create("return 3;", 'z')
	];
}