namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPowerOfTwoTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 16),
		Create("return false;", 18),
	];

	public override string TestMethod => """
		bool IsPowerOfTwo(int n)
		{
			if (n <= 0)
			{
				return false;
			}
			return (n & (n - 1)) == 0;
		}
		""";
}

