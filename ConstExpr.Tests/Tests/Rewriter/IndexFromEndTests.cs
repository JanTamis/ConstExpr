using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for index-from-end conversion (IFE).
///   Indexing off the end of a collection via a subtraction chain rooted at
///   <c>receiver.Length</c>/<c>.Count</c> is rewritten to index-from-end syntax (<c>receiver[^offset]</c>).
///   Note: IFE runs after ConstExprPartialRewriter, so inputs are Unknown — the array and index
///   stay symbolic and the subtraction chain survives partial evaluation.
/// </summary>
[InheritsTests]
public class IndexFromEndTests() : BaseTest<Func<int[], int, int>>(optimizations: OptimizationFlags.IndexFromEndConversion)
{
	public override string TestMethod => GetString((numbers, i) => numbers[numbers.Length - 1 - i]);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numbers, i) => numbers[^(1 + i)], [ Unknown, Unknown ])
	];
}