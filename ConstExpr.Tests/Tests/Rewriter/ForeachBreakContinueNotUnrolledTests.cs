namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Unrolling turns the loop's break into a goto, but there is no jump target for a continue once
///   the loop is gone. So a body that mixes a runtime-guarded continue with a break is NOT unrolled
///   — it falls back to a real foreach rather than emit an orphaned continue.
/// </summary>
[InheritsTests]
public class ForeachBreakContinueNotUnrolledTests : BaseTest<Func<char, char, int>>
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
		CreateDefault()
	];
}