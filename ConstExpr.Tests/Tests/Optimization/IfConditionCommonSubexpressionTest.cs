using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

// Regression: `numbers.Length` appears in an if-condition and again inside that if's own body (see
// SecondLargest in ConstExpr.Sample). The body is a separate BlockSyntax, and CSE previously scoped
// candidate-collection strictly per block, so the two occurrences were never counted together and
// nothing got hoisted. An if/else branch runs exactly when its own Condition (just evaluated)
// implies it, so folding a repeat inside the branch into the same hoist as an unconditional
// Condition occurrence is safe.
[InheritsTests]
public class IfConditionCommonSubexpressionTest() : BaseTestWithRandomValues<Func<int[], int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString(numbers =>
	{
		if (numbers.Length < 2)
		{
			return numbers.Length == 1 ? numbers[0] : 0;
		}

		var sum = 0;

		foreach (var n in numbers)
		{
			sum += n;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(numbers =>
		{
			var numbersLength = numbers.Length;

			if (numbersLength < 2)
			{
				return numbersLength == 1 ? numbers[0] : 0;
			}

			var sum = 0;

			foreach (var n in numbers)
			{
				sum += n;
			}

			return sum;
		}),
	];
}