using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

// Guards the generalized MergeRedundantInitializers: a dead constant init (`double x = 0`) folds into a
// later top-level assignment even when other statements sit in between (here `var y = a + a;`). The merge
// preserves the incoming declaration verbatim, so the double-carrying literals (2D/4D) survive and the
// result stays real division.
[InheritsTests]
public class DeadInitializerMergeTest() : BaseTest<Func<int, double>>(optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(a =>
	{
		double x = 0;
		var y = a + a;
		x = y;

		return x / 2 + x / 4;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a =>
		{
			var x = a + a;

			return x / 2D + x / 4D;
		})
	];
}