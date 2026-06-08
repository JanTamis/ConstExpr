using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class GCDTest() : BaseTest<Func<int, int, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) =>
	{
		a = System.Math.Abs(a);
		b = System.Math.Abs(b);

		while (b != 0)
		{
			var temp = b;
			b = a % b;
			a = temp;
		}

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			a = AbsFast(a);
			b = AbsFast(b);

			while (b != 0)
			{
				var temp = b;

				b = a % b;
				a = temp;
			}

			return a;
			"""),
		Create((_, _) => 6, [ 48, 18 ]),
		Create((_, _) => 1, [ 17, 19 ]),
		Create((_, _) => 15, [ 45, 60 ])
	];
}