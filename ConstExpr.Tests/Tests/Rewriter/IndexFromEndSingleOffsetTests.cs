using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Base case (single subtraction, no literal offset): no parens needed around a lone identifier.
/// </summary>
[InheritsTests]
public class IndexFromEndSingleOffsetTests() : BaseTest<Func<int[], int, int>>(optimizations: OptimizationFlags.IndexFromEndConversion)
{
	public override string TestMethod => GetString((numbers, i) => numbers[numbers.Length - i]);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numbers, i) => numbers[^i], [ Unknown, Unknown ])
	];
}