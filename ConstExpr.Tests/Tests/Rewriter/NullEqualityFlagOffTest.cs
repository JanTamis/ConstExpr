using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullEqualityFlagOffTest() : BaseTestWithRandomValues<Func<string, bool>>(optimizations: OptimizationFlags.All & ~OptimizationFlags.UseNullableAnnotations)
{
	public override string TestMethod => GetString(s => s == null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}