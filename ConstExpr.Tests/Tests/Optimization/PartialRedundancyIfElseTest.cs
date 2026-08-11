using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Partial redundancy across an exhaustive if/else: `numbers.Length` occurs in both branches and in
///   neither the condition nor at statement top level, so no occurrence is unconditional and CSE used
///   to leave it alone. Exactly one branch runs and both read it, so it is evaluated exactly once
///   either way — hoisting it in front of the `if` forces nothing extra and is free even though a
///   member access can throw.
///   <para>
///     The then-branch holds a loop on purpose. A branch of nothing but assignments or returns gets
///     canonicalised into a ternary well before CSE runs, which would route this through the ternary
///     arm of the every-path rule (already covered by <see cref="PartialRedundancyTernaryTest" />)
///     and leave the if/else arm untested.
///   </para>
/// </summary>
[InheritsTests]
public class PartialRedundancyIfElseTest() : BaseTestWithRandomValues<Func<int[], bool, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((numbers, flag) =>
	{
		var sum = 0;

		if (flag)
		{
			foreach (var x in numbers)
			{
				sum += x;
			}

			sum += numbers.Length;
		}
		else
		{
			sum -= numbers.Length;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numbers, flag) =>
		{
			var sum = 0;
			var numbersLength = numbers.Length;

			if (flag)
			{
				foreach (var x in numbers)
				{
					sum += x;
				}

				sum += numbersLength;
			}
			else
			{
				sum -= numbersLength;
			}

			return sum;
		}),
	];
}