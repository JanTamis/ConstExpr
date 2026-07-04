using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

// Regression: the hoisted CSE variable must not collide with an identifier that already exists in
// the method. `prod * y` is named "prod" by GenerateName, and "prod" is already a parameter, so a
// buggy pass emits `var prod = …` that redeclares the parameter (non-compiling). Colliding against a
// parameter (rather than a local) guarantees the name survives the pre-CSE dead-code prune. The fix
// seeds the used-name set from existing identifiers, producing `prod2`.
[InheritsTests]
public class CommonSubexpressionNameCollisionTest() : BaseTest<Func<int, int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination)
{
	public override string TestMethod => GetString((prod, y) =>
	{
		var a = prod * y;
		var b = prod * y;
		return a * b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((prod, y) =>
		{
			var prod2 = prod * y;
			return prod2 * prod2;
		}),
		Create((_, _) => 100, [ 2, 5 ])
	];
}