using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Vectorization;

// Regression guard for the AutoVectorization pass: enabling it must not disturb constant folding.
// When every argument is a compile-time constant the loop is folded away before the vectorization
// pass runs, so the pass is a no-op and the folded literal is produced.
//
// A golden assertion on the emitted SIMD helper (for non-constant inputs) is intentionally not
// authored here: the generated helper method carries a content-hash-based name, so its exact text
// cannot be predicted without building. Inspect Vectorize/ConstExpr.Sample/Generated/ to see it.
[InheritsTests]
public class VectorizedSumTest() : BaseTest<Func<int[], int>>(FastMathFlags.All, optimizations: OptimizationFlags.AutoVectorization)
{
	public override string TestMethod => GetString(arr =>
	{
		var sum = 0;

		foreach (var num in arr)
		{
			sum += num;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 15, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ]),
		Create(_ => 42, [ new[] { 42 } ])
	];
}
