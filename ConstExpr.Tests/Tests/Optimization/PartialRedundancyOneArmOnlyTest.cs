using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Negative test for the partial-redundancy rule: `n * n` occurs twice, but each occurrence sits in
///   an `if` with no `else`. Neither construct is exhaustive — the fall-through path evaluates nothing —
///   so a then-only occurrence proves nothing and hoisting would compute `n * n` where the original
///   never did. This is what the `Else is not null` requirement guards. The body must come out unchanged.
///   <para>
///     `n * n` rather than a scalable multiply: `n * 3` would be strength-reduced to `(n &lt;&lt; 1) + n`
///     by an unrelated always-on peephole, and the test would then be asserting that peephole's output
///     instead of this pass's refusal.
///   </para>
/// </summary>
[InheritsTests]
public class PartialRedundancyOneArmOnlyTest() : BaseTest<Func<int, bool, bool, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((n, flag, other) =>
	{
		var sum = 0;

		if (flag)
		{
			sum += n * n;
		}

		if (other)
		{
			sum += n * n;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((n, flag, other) =>
		{
			var sum = 0;

			if (flag)
			{
				sum += n * n;
			}

			if (other)
			{
				sum += n * n;
			}

			return sum;
		}),
		Create((_, _, _) => 8, [ 2, true, true ]),
		Create((_, _, _) => 4, [ 2, true, false ]),
		Create((_, _, _) => 0, [ 2, false, false ])
	];
}