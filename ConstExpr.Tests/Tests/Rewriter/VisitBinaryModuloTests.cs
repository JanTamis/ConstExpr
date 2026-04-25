namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for modulo optimizer strategies.</summary>
[InheritsTests]
public class ModuloByOneTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x % 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 0;"),
		Create("return 0;", 42),
		Create("return 0;", -7),
	];
}

/// <summary>x % x = 0 when x != 0.</summary>
[InheritsTests]
public class ModuloIdempotencyTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x % x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x % x;"),
		Create("return 0;", 7),
		Create("return 0;", -3),
	];
}

/// <summary>x % power-of-two → x &amp; (pow - 1) for unsigned types.</summary>
[InheritsTests]
public class ModuloByPowerOfTwoTest : BaseTest<Func<uint, uint>>
{
	public override string TestMethod => GetString(x => x % 8u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x & 7u;"),
		Create("return 3u;", 11u),
		Create("return 0u;", 8u),
	];
}
