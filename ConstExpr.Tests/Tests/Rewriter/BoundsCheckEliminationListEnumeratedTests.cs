using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A write through the span skips the <c>_version</c> bump the list's indexer does, so a list that
///   is enumerated anywhere is refused — otherwise this would stop throwing and silently mutate.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationListEnumeratedTests() : BaseTest<Func<List<int>, int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString(values =>
	{
		var sum = 0;

		// Refused purely because the list is enumerated — the indexed read would otherwise qualify.
		foreach (var value in values)
		{
			sum += value + values[0];
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}