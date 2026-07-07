using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The <c>Length</c> receiver must match the indexed receiver structurally. Two distinct arrays
///   must not be rewritten, since the offset isn't actually relative to the indexed collection.
/// </summary>
[InheritsTests]
public class IndexFromEndDifferentReceiverNotRewrittenTests() : BaseTest<Func<int[], int[], int, int>>(optimizations: OptimizationFlags.IndexFromEndConversion)
{
	public override string TestMethod => GetString((numbers, other, i) => numbers[other.Length - 1 - i]);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numbers, other, i) => numbers[other.Length - 1 - i], [ Unknown, Unknown, Unknown ])
	];
}