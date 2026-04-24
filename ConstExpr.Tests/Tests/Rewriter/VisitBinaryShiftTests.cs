namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>Tests for binary shift strategies.</summary>
[InheritsTests]
public class ShiftByZeroTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x =>
	{
		var a = x << 0;
		var b = x >> 0;
		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// x << 0 = x, x >> 0 = x → x + x → x << 1
		Create("return x << 1;"),
		Create("return 10;", 5),
		Create("return -6;", -3),
	];
}

/// <summary>Tests for left-shift of zero: 0 << n = 0.</summary>
[InheritsTests]
public class ShiftZeroTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		return 0 << n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 0;"),
		Create("return 0;", 5),
		Create("return 0;", 0),
	];
}
