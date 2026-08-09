namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class GCDTest : BaseTest<Func<int, int, int>>
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
			a = FastAbs(a);
			b = FastAbs(b);

			while (b != 0)
			{
				var temp = b;

				b = a % b;
				a = temp;
			}

			return a;
			"""),
		CreateFolded(48, 18),
		CreateFolded(17, 19),
		CreateFolded(45, 60)
	];
}