using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceFlagOffTest() : BaseTest<Func<string, string, string>>(optimizations: OptimizationFlags.All & ~OptimizationFlags.UseNullableAnnotations)
{
	public override string TestMethod => GetString((a, b) => a ?? b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded("hello", "world")
	];
}