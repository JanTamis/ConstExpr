using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsEvenTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0)
		{
			n = -n;
		}

		return (n & 1) == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			if (n < 0)
			{
				n = -n;
			}

			return Int32.IsEvenInteger(n);
		}),
		Create(_ => true, [ 4 ]),
		Create(_ => false, [ 5 ])
	];
}