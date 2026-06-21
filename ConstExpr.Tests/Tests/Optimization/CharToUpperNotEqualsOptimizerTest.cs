using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CharToUpperNotEqualsOptimizerTest() : BaseTest<Func<char, char, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((left, right) =>
	{
		return Char.ToUpper(left) != Char.ToUpper(right);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((left, right) => !left.Equals(right, StringComparison.CurrentCultureIgnoreCase)),
		Create((_, _) => false, [ 'a', 'A' ]),
		Create((_, _) => true, [ 'a', 'B' ])
	];
}