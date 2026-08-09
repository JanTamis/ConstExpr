namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class LCMTest : BaseTest<Func<int, int, int>>
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			if (a == 0 || b == 0)
			{
				return 0;
			}

			var aa = FastAbs(a);
			var bb = FastAbs(b);

			while (bb != 0)
			{
				var temp = bb;

				bb = aa % bb;
				aa = temp;
			}

			return FastAbs(a * b) / aa;
			"""),
		CreateFolded(4, 6),
		CreateFolded(0, 5),
		CreateFolded(21, 6)
	];
}