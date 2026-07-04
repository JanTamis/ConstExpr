using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for Loop Unswitching (LU).
///   A loop whose body is a single <c>if</c> with a loop-invariant condition is duplicated per
///   branch with the condition hoisted out of the loop, so the test runs once instead of on
///   every iteration.
///   Note: LU runs after ConstExprPartialRewriter, so inputs are Unknown — the loop bound is
///   unknown (not unrolled) and the condition (a bare parameter) stays a runtime invariant that
///   survives partial evaluation.
/// </summary>
[InheritsTests]
public class LoopUnswitchingTests() : BaseTest<Func<int, bool, int>>(optimizations: OptimizationFlags.LoopUnswitching)
{
	/// <summary>
	///   The loop body is a single if whose condition (<c>flag</c>) is invariant across iterations.
	/// </summary>
	public override string TestMethod => GetString((n, flag) =>
	{
		var result = 0;

		for (var i = 0; i < n; i++)
		{
			if (flag)
			{
				result += i;
			}
			else
			{
				result -= i;
			}
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown inputs: the loop is not unrolled and `flag` stays a runtime invariant, so the
		// branch is hoisted out and the loop is duplicated per arm.
		Create((n, flag) =>
		{
			var result = 0;

			if (flag)
			{
				for (var i = 0; i < n; i++)
				{
					result += i;
				}
			}
			else
			{
				for (var i = 0; i < n; i++)
				{
					result -= i;
				}
			}

			return result;
		}, [ Unknown, Unknown ])
	];
}