using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CharToLowerEqualsOptimizerTest() : BaseTest<Func<char, char, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((left, right) =>
	{
		return Char.ToLower(left) == Char.ToLower(right);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((left, right) => left.Equals(right, StringComparison.CurrentCultureIgnoreCase)),
		Create((_, _) => true, [ 'A', 'a' ]),
		Create((_, _) => false, [ 'A', 'b' ])
	];
}