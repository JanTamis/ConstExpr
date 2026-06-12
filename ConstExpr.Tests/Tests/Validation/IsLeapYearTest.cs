using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsLeapYearTest() : BaseTest<Func<int, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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
		Create(year =>
		{
			var rshift = (int) ((long) year * 1374389535 >> 32) >> 5;
			var rshift_2 = (int) ((long) year * 1374389535 >> 32) >> 7;

			return (year & 3) == 0 && year - (rshift + (rshift >>> 31)) * 100 != 0 && year - (rshift_2 + (rshift_2 >>> 31)) * 400 == 0;
		}),
		Create(_ => true, [ 2000 ]),
		Create(_ => false, [ 1900 ])
	];
}