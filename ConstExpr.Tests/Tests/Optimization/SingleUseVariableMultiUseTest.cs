namespace ConstExpr.Tests.Optimization;

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