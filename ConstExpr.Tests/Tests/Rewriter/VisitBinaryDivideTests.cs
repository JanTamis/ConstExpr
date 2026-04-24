namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>Tests for divide optimizer strategies.</summary>
[InheritsTests]
public class DivideByOneTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x / 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x;"),
		Create("return 9;", 9),
		Create("return -4;", -4),
	];
}

/// <summary>x / x = 1 when x != 0 (idempotency).</summary>
[InheritsTests]
public class DivideIdempotencyTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x / x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 1;"),
		Create("return 1;", 5),
		Create("return 1;", -3),
	];
}

/// <summary>0 / x = 0 when x != 0.</summary>
[InheritsTests]
public class DivideZeroByNonZeroTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => 0 / x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 0;"),
		Create("return 0;", 7),
		Create("return 0;", -2),
	];
}
