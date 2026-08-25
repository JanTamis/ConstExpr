namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class TempCompoundConditionalDoubleTest : BaseTestWithRandomValues<Func<bool, double>>
{
	public override string TestMethod => GetString((bool c) =>
	{
		double x = 0D;

		if (c || !c)
		{
			x = c ? 1 : 3;
			x /= 2;
		}

		return x;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => c ? 999D : 999D)
	];
}

[InheritsTests]
public class TempCompoundConditionalByteTest : BaseTestWithRandomValues<Func<bool, byte>>
{
	public override string TestMethod => GetString((bool c) =>
	{
		byte b = 0;

		if (c || !c)
		{
			b = c ? (byte) 200 : (byte) 100;
			b += 100;
		}

		return b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => (byte) 255)
	];
}