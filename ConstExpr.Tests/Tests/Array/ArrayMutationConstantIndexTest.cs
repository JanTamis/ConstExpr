using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayMutationConstantIndexTest() : BaseTest<Func<int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(() =>
	{
		var counts = new int[256];

		foreach (var c in "aba")
		{
			counts[c]++;
		}

		return counts['a'];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(() =>
		{
			var counts = new int[256];

			counts['a']++;
			counts['b']++;
			counts['a']++;

			return counts['a'];
		})
	];
}