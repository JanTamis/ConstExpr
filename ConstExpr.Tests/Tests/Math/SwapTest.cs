namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SwapTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return (20, 10);", 10, 20),
		Create("return (0, 42);", 42, 0),
		Create("return (-5, 5);", 5, -5),
	];

	public override string TestMethod => """
		(int, int) Swap(int a, int b)
		{
			var temp = a;
			a = b;
			b = temp;
			return (a, b);
		}
		""";
}

