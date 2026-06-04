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
		Create(n => n + 1, [ Unknown ]), // Unknown n → temp is inlined
		Create(_ => 6, [ 5 ]), // Constant 5 → folded to 6
	];
}