using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   An index-from-end is a different indexer than <c>this[int]</c>, so it carries no offset the
///   pass can hand to <c>Unsafe.Add</c>. It must be left untouched.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationIndexFromEndTests() : BaseTest<Func<int[], int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString(numbers => numbers[^1]);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}