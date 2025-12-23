using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class MinArrayTest() : BaseTest<Func<int[], int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			if (values.Length == 0)
			{
				return 2147483647;
			}

			var min = 2147483647;

			foreach (var v in values)
			{
				if (v < min)
				{
					min = v;
				}
			}

			return min;
			""", Unknown),
		Create("return 3;", new[] { 5, 4, 3, 9 }),
		Create("return 1;", new[] { 7, 2, 1, 8 }),
		Create("return 2147483647;", System.Array.Empty<int>())
	];
}