using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class MinArrayTest() : BaseTest<Func<int[], int>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(values =>
	{
		if (values.Length == 0)
		{
			return Int32.MaxValue;
		}

		var min = Int32.MaxValue;

		foreach (var v in values)
		{
			if (v < min)
			{
				min = v;
			}
		}

		return min;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(values =>
		{
			if (values.Length == 0)
				return Int32.MaxValue;

			var min = Int32.MaxValue;

			foreach (var v in values)
			{
				if (v < min)
					min = v;
			}

			return min;
		}),
		Create(_ => 3, [ new[] { 5, 4, 3, 9 } ]),
		Create(_ => 1, [ new[] { 7, 2, 1, 8 } ]),
		Create(_ => Int32.MaxValue, [ System.Array.Empty<int>() ])
	];
}