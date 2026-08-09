namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class ReverseNumberTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var originalN = n;
		var reversed = 0;

		n = System.Math.Abs(n);

		while (n > 0)
		{
			reversed = reversed * 10 + n % 10;
			n /= 10;
		}

		return Int32.CopySign(reversed, originalN);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var originalN = n;
			var reversed = 0;

			n = FastAbs(n);

			while (n > 0)
			{
				reversed = reversed * 10 + n % 10;
				n /= 10;
			}

			return FastCopySign(reversed, originalN);
			"""),
	];
}