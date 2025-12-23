using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class LCMTest() : BaseTest<Func<int, int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((a, b) =>
	{
		if (a == 0 || b == 0)
		{
			return 0;
		}

		var aa = System.Math.Abs(a);
		var bb = System.Math.Abs(b);

		while (bb != 0)
		{
			var temp = bb;
			bb = aa % bb;
			aa = temp;
		}

		var gcd = aa;

		return System.Math.Abs(a * b) / gcd;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			if (a == 0 || b == 0)
			{
				return 0;
			}
			
			var aa = Int32.Abs(a);
			var bb = Int32.Abs(b);
			
			while (bb != 0)
			{
				var temp = bb;
			
				bb = aa % bb;
				aa = temp;
			}
			
			var gcd = aa;
			
			return Int32.Abs(a * b) / gcd;
			""", Unknown, Unknown),
		Create("return 12;", 4, 6),
		Create("return 0;", 0, 5),
		Create("return 42;", 21, 6)
	];
}