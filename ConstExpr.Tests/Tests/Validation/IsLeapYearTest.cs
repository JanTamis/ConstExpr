using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsLeapYearTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(year =>
	{
		if (year % 4 != 0)
		{
			return false;
		}

		if (year % 100 != 0)
		{
			return true;
		}

		return year % 400 == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(year => (year & 3) == 0 && year % 100 != 0 && year % 400 == 0),
		Create(_ => true, [ 2000 ]),
		Create(_ => false, [ 1900 ])
	];
}