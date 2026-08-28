namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Contains() optimization with complex lambda expressions
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationComplexTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Multiple chained operations before Contains
		var a = x.Where(v => v > 0).Distinct().OrderBy(v => v).Contains(5) ? 1 : 0;

		// Select with more complex expression
		var b = x.Select(v => v + 10).Concat(x).Contains(15) ? 1 : 0;

		// Where with complex predicate
		var c = x.Where(v => v % 2 == 0).Contains(4) ? 1 : 0;

		// Nested operations
		var d = x.Distinct().Where(v => v < 10).Contains(5) ? 1 : 0;

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var callVal = VectorOperations.Any<int, ContainsOperatorCwN6KQ>(x);

			return (Unsafe.BitCast<bool, byte>(callVal) << 1) + Unsafe.BitCast<bool, byte>(callVal || VectorOperations.Any<int, ContainsOperator_7Nasw>(x)) + Unsafe.BitCast<bool, byte>(VectorOperations.Any<int, ContainsOperator2AN6KQ>(x));
			""", Unknown),
	];
}