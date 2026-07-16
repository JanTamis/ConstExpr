using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Negative test for strength reduction: the multiplier is a runtime variable, not an integer
///   literal, so no accumulator step can be computed. The pass must leave the loop unchanged.
/// </summary>
[InheritsTests]
public class StrengthReductionNonConstantMultiplierTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.InductionVariableStrengthReduction)
{
	public override string TestMethod => GetString((n, m) =>
	{
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			sum += i * m;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}