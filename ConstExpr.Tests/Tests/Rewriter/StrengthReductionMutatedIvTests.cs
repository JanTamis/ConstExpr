using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Negative test for strength reduction: the loop counter is also written inside the body, so
///   an accumulator advanced only by the incrementor would desynchronize from <c>i * c</c>. The
///   pass must leave the loop unchanged.
/// </summary>
[InheritsTests]
public class StrengthReductionMutatedIvTests() : BaseTest<Func<int, int>>(optimizations: OptimizationFlags.InductionVariableStrengthReduction)
{
	public override string TestMethod => GetString(n =>
	{
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			sum += i * 10;
			i += 1;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}