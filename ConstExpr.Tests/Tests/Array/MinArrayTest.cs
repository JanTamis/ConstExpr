using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class MinArrayTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
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
		Create("return 2147483647;", System.Array.Empty<int>()),
	];

	public override string TestMethod => """
		int MinArray(int[] values)
		{
			if (values.Length == 0)
			{
				return int.MaxValue;
			}

			var min = int.MaxValue;

			foreach (var v in values)
			{
				if (v < min)
				{
					min = v;
				}
			}

			return min;
		}
		""";
}

