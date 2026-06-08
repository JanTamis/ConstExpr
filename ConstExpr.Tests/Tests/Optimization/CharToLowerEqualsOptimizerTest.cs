using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CharToLowerEqualsOptimizerTest() : BaseTest<Func<char, char, bool>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((left, right) =>
	{
		return char.ToLower(left) == char.ToLower(right);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown),
		Create((_, _) => true, [ 'A', 'a' ]),
		Create((_, _) => false, [ 'A', 'b' ]),
	];
}