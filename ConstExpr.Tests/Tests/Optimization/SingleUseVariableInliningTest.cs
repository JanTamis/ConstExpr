namespace ConstExpr.Tests.Optimization;

/// <summary>
/// Tests for single-use variable inlining.
/// When a local variable is assigned exactly once and read exactly once,
/// it should be inlined at the usage site and its declaration removed.
/// </summary>
[InheritsTests]
public class SingleUseVariableSimpleTest : BaseTest<Func<int, int>>
{
	/// <summary>
	/// var temp = n + 1; return temp;
	/// → temp used once, no re-assignment → should be inlined: return (n + 1);
	/// </summary>
	public override string TestMethod => GetString(n =>
	{
		var temp = n + 1;
		return temp;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return n + 1;", Unknown), // Unknown n → temp is inlined
		Create("return 6;", 5),             // Constant 5 → folded to 6
	];
}

/// <summary>
/// Tests chained single-use variable inlining.
/// </summary>
[InheritsTests]
public class SingleUseVariableChainTest : BaseTest<Func<int, int>>
{
	/// <summary>
	/// var a = n + 1; var b = a * 2; return b;
	/// → a is used once (in b's init), b is used once (in return)
	/// → both should be inlined: return ((n + 1) * 2);
	/// </summary>
	public override string TestMethod => GetString(n =>
	{
		var a = n + 1;
		var b = a * 2;
		return b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (n + 1) << 1;", Unknown), // Both a and b inlined; * 2 → << 1
		Create("return 12;", 5),                   // (5 + 1) * 2 = 12
	];
}

/// <summary>
/// Variables used more than once must NOT be inlined.
/// </summary>
[InheritsTests]
public class SingleUseVariableMultiUseTest : BaseTest<Func<int, int>>
{
	/// <summary>
	/// var temp = n + 1; return temp + temp;
	/// → temp is used TWICE → must NOT be inlined.
	/// </summary>
	public override string TestMethod => GetString(n =>
	{
		var temp = n + 1;
		return temp + temp;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown), // temp used twice → body unchanged
		Create("return 12;", 5), // (5+1)+(5+1) = 12 → constant-folded
	];
}


