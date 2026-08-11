namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Tests chained single-use variable inlining.
/// </summary>
[InheritsTests]
public class SingleUseVariableChainTest : BaseTestWithRandomValues<Func<int, int>>
{
	/// <summary>
	///   var a = n + 1; var b = a * 2; return b;
	///   → a is used once (in b's init), b is used once (in return)
	///   → both should be inlined: return ((n + 1) * 2);
	/// </summary>
	public override string TestMethod => GetString(n =>
	{
		var a = n + 1;
		var b = a * 2;
		return b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => n + 1 << 1, [ Unknown ]),
	];
}