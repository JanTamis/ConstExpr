using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Negative test for the partial-redundancy rule: `k + 2` does occur in both arms of an exhaustive
///   if/else, but one arm increments `k` inside a loop body first, so the two occurrences do not have
///   the same value and hoisting would use the pre-loop sum on the `flag` path.
///   <para>
///     The ordinary `mutatedNames` guard does not see this: ExpressionCollector stops at nested blocks,
///     so the `k++` inside the `for` body never reaches it. Only the every-path rule's own deep
///     mutation scan catches it — which is why the loop bound is the unknown parameter `n` rather than
///     a constant. A constant bound would be unrolled, lifting `k++` up into the arm's statement list
///     where `mutatedNames` already covers it, and this would silently stop testing the deep scan.
///   </para>
///   <para>
///     DO NOT "simplify" this body — three details are load-bearing, and each has already been
///     tripped over once:
///     <list type="bullet">
///       <item>
///         <description>
///           <b>The <c>else</c> must stay</b>, even though an IDE cleanup will offer to drop it as
///           redundant after the <c>return</c>. Without it the construct is not exhaustive, so the
///           every-path rule bails at its `Else is not null` check before the mutation scan is ever
///           consulted — and the trailing occurrence becomes unconditional, which the pre-existing CSE
///           rule then hoists on its own (`var sum = k + 2;`). The test fails for a reason that has
///           nothing to do with this pass.
///         </description>
///       </item>
///       <item>
///         <description>
///           <b>The arms must stay different</b> (`k + 2` vs `(k + 2) * n`). Two identical arms get
///           sunk out of the branches and the if/else dissolves before CSE ever sees two occurrences.
///         </description>
///       </item>
///       <item>
///         <description>
///           <b>No result local</b>: a `T x = literal;` assigned in every branch has its initializer
///           elided into `var x;`, which is not valid C# and cannot be written as an expected body.
///           Returning from both arms avoids the declaration entirely.
///         </description>
///       </item>
///     </list>
///   </para>
///   `k` is seeded from `n`, not a literal, for a separate reason: a known-constant `k` makes `k + 2`
///   foldable and the fold removes the candidate before this pass ever sees it.
///   <para>The body must come out unchanged.</para>
/// </summary>
[InheritsTests]
public class PartialRedundancyMutatedInArmTest() : BaseTest<Func<int, bool, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((n, flag) =>
	{
		var k = n;

		if (flag)
		{
			for (var j = 0; j < n; j++)
			{
				k++;
			}

			return k + 2;
		}
		return (k + 2) * n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((n, flag) =>
		{
			var k = n;

			if (flag)
			{
				for (var j = 0; j < n; j++)
				{
					k++;
				}

				return k + 2;
			}

			var sum = k + 2;

			return sum * n;
		}),
		Create((_, _) => 8, [ 3, true ]),
		Create((_, _) => 15, [ 3, false ])
	];
}