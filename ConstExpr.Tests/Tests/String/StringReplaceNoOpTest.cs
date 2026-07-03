using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>s.Replace("x","x") with same old/new is a no-op.</summary>
[InheritsTests]
public class StringReplaceNoOpTest() : BaseTest<Func<string, string>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(s => s.Replace("a", "a"));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s)
	];
}