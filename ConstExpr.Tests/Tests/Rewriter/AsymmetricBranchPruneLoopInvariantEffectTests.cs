using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A loop body whose if/else branches assign two DIFFERENT outer-scope variables (rather than the
///   same one via a compound assignment, as in <see cref="IfElseLoopInvariantEffectTests" />) must
///   not have either branch silently dropped. <c>a = 1;</c> looks — from inside
///   <c>HoistCommonBranchAssignments</c>'s narrow, loop-body-only view — like a dead write, since
///   neither <c>a</c> nor <c>b</c> is read anywhere inside that sub-block; only the return statement
///   after the loop reads them. <c>b = x;</c> is never mistaken for dead (its RHS isn't a literal),
///   which is exactly what makes this a useful regression case: pruning exactly one branch (not both)
///   corrupts the if-statement by dropping the other branch entirely, rather than the two-branches-
///   pruned case silently self-correcting because nothing gets spliced back in.
/// </summary>
[InheritsTests]
public class AsymmetricBranchPruneLoopInvariantEffectTests() : BaseTestWithRandomValues<Func<int, int, bool, int>>(optimizations: OptimizationFlags.None)
{
	public override string TestMethod => GetString((n, x, flag) =>
	{
		var a = 0;
		var b = 0;

		for (var i = 0; i < n; i++)
		{
			if (flag)
			{
				a = 1;
			}
			else
			{
				b = x;
			}
		}

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((n, x, flag) =>
		{
			var a = 0;
			var b = 0;

			for (var i = 0; i < n; i++)
			{
				if (flag)
				{
					a = 1;
				}
				else
				{
					b = x;
				}
			}

			return a + b;
		})
	];
}