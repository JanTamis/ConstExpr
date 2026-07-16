using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for induction-variable strength reduction (SR).
///   <c>i * 10</c> inside a counted loop becomes an accumulator advanced by 10 alongside the
///   counter, replacing a multiply per iteration with an add. The constant 10 is deliberately
///   not a power of two or 2^n±1, so the always-on multiply strategies leave the product intact
///   for this pass.
/// </summary>
[InheritsTests]
public class StrengthReductionTests() : BaseTest<Func<int, int>>(optimizations: OptimizationFlags.InductionVariableStrengthReduction)
{
	public override string TestMethod => GetString(n =>
	{
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			sum += i * 10;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown bound: the loop survives partial evaluation and the product is reduced.
		Create(n =>
		{
			var sum = 0;

			for (int i = 0, iMul10 = 0; i < n; i++, iMul10 += 10)
			{
				sum += iMul10;
			}

			return sum;
		}, [ Unknown ]),

		// Known bound folds completely through the interpreter: 10 * (0+1+2+3+4) = 100. This
		// anchors the semantics of the shape independently of the rewriter.
		Create(_ => 100, [ 5 ])
	];
}