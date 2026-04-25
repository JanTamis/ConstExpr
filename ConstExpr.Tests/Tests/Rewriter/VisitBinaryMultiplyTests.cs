namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for multiply optimizer strategies.</summary>
[InheritsTests]
public class MultiplyByZeroTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x * 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 0;"),
		Create("return 0;", 99),
		Create("return 0;", -5),
	];
}

/// <summary>x * 1 = x.</summary>
[InheritsTests]
public class MultiplyByOneTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x * 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x;"),
		Create("return 7;", 7),
		Create("return -2;", -2),
	];
}

/// <summary>x * 2 → x + x (strength reduction) or x &lt;&lt; 1.</summary>
[InheritsTests]
public class MultiplyByTwoTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x * 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x << 1;"),
		Create("return 10;", 5),
		Create("return -6;", -3),
	];
}

/// <summary>x * -1 → -x.</summary>
[InheritsTests]
public class MultiplyByNegativeOneTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x * -1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -x;"),
		Create("return -7;", 7),
		Create("return 3;", -3),
	];
}
