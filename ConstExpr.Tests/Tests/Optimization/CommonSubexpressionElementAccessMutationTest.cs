using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

// Regression: `arr[k]` is read, then `arr` is written via an indexer, then `arr[k]` is read again.
// The two reads are NOT the same value, so CSE must not merge them. A buggy pass tracks only plain
// identifier assignments as mutations and misses the indexer write, hoisting a single `arr[k]`.
[InheritsTests]
public class CommonSubexpressionElementAccessMutationTest() : BaseTest<Func<int[], int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((arr, k) =>
	{
		var x = arr[k];
		arr[0] = 9;
		var y = arr[k];
		return x + x + y + y;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((arr, k) =>
		{
			var x = arr[k];
			arr[0] = 9;
			var y = arr[k];
			return x * 2 + y * 2;
		})
	];
}