using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ConditionalAccessFlagOffTest() : BaseTestWithRandomValues<Func<string, string?>>(optimizations: OptimizationFlags.All & ~OptimizationFlags.UseNullableAnnotations)
{
	// ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract — being non-nullable is the point.
	public override string TestMethod => GetString(s => s?.Trim());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}