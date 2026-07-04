using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

// Coverage: `x + y` and `y + x` are the same subexpression by commutativity and should be hoisted
// once. Uses FastMathFlags.Strict on purpose — commutation (a 2-operand swap) is exact for both
// integers and IEEE-754 floats, so this must NOT depend on fast-math being enabled.
[InheritsTests]
public class CommutativeCommonSubexpressionTest() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((x, y) =>
	{
		var a = x + y;
		var b = y + x;
		return a * b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) =>
		{
			var sum = x + y;
			return sum * sum;
		}),
		Create((_, _) => 25, [ 2, 3 ])
	];
}