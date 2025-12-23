using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
/// Tests for pattern matching bitmask optimization.
/// Verifies that patterns like "x is 1 or 5 or 10 or 15 or 20" are optimized
/// into efficient bitmask checks.
/// </summary>
[InheritsTests]
public class PatternBitmaskTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		// Pattern: n is 1 or 5 or 10 or 15 or 20
		// Should optimize to: (uint)n < 21u && ((1082402u >> n) & 1) != 0
		return n is 1 or 5 or 10 or 15 or 20;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return (uint)(n - 1) <= 19U && (0x108422 & 1 << n - 1) != 0;", Unknown), // Unknown value
		Create("return true;", 1),   // Match
		Create("return true;", 5),   // Match
		Create("return true;", 10),  // Match
		Create("return true;", 15),  // Match
		Create("return true;", 20),  // Match
		Create("return false;", 0),  // No match
		Create("return false;", 3),  // No match
		Create("return false;", 7),  // No match
		Create("return false;", 21), // No match
	];
}

/// <summary>
/// Test with larger set of values
/// </summary>
[InheritsTests]
public class PatternBitmaskLargeTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 0 or 10 or 20 or 30 or 40 or 50 or 60;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return (uint)n <= 60U && n % 10 == 0;", Unknown),
		Create("return true;", 0),
		Create("return true;", 10),
		Create("return true;", 20),
		Create("return true;", 30),
		Create("return true;", 40),
		Create("return true;", 50),
		Create("return true;", 60),
		Create("return false;", 5),
		Create("return false;", 25),
	];
}

/// <summary>
/// Test with small set (powers of 2)
/// </summary>
[InheritsTests]
public class PatternBitmaskSmallTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 2 or 4 or 8;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return (uint)(n - 2) <= 6U && (0x45 & 1 << n - 2) != 0;", Unknown),
		Create("return true;", 2),
		Create("return true;", 4),
		Create("return true;", 8),
		Create("return false;", 1),
		Create("return false;", 3),
		Create("return false;", 5),
	];
}

/// <summary>
/// Test with consecutive values
/// </summary>
[InheritsTests]
public class PatternBitmaskConsecutiveTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 5 or 6 or 7 or 8;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return (uint)(n - 5) <= 3U;", Unknown),
		Create("return true;", 5),
		Create("return true;", 6),
		Create("return true;", 7),
		Create("return true;", 8),
		Create("return false;", 4),
		Create("return false;", 9),
	];
}

/// <summary>
/// Test with byte values
/// </summary>
[InheritsTests]
public class PatternBitmaskByteTest() : BaseTest<Func<byte, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 1 or 3 or 7;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return n - 1 <= 6 && (0x45 & 1 << n - 1) != 0;", Unknown),
		Create("return true;", (byte)1),
		Create("return true;", (byte)3),
		Create("return true;", (byte)7),
		Create("return false;", (byte)0),
		Create("return false;", (byte)4),
	];
}

