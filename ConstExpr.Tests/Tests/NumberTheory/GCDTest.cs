using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class GCDTest() : BaseTest<Func<int, int, int>>(FastMathFlags.FastMath)
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
		Create("return 6;", 48, 18),
		Create("return 1;", 17, 19),
		Create("return 15;", 45, 60)
	];
}