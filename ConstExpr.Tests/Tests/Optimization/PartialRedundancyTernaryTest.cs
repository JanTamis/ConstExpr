using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   The same partial-redundancy rule as <see cref="PartialRedundancyIfElseTest" />, one level down:
///   both arms of a ternary read `numbers.Length`. The existing rule refuses to hoist out of a ternary
///   arm because the other arm may exist specifically to avoid the cost — but that argument is about a
///   candidate in ONE arm. In both arms nothing is forced: it already ran whichever way the branch went.
/// </summary>
[InheritsTests]
public class PartialRedundancyTernaryTest() : BaseTestWithRandomValues<Func<int[], bool, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((numbers, flag) =>
	{
		return flag ? numbers.Length + 2 : numbers.Length + 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numbers, flag) =>
		{
			var numbersLength = numbers.Length;

			return flag ? numbersLength + 2 : numbersLength + 1;
		}),
	];
}