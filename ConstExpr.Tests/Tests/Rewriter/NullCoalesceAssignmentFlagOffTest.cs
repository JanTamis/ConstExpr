using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceAssignmentFlagOffTest() : BaseTest<Func<string, string, string>>(optimizations: OptimizationFlags.All & ~OptimizationFlags.UseNullableAnnotations)
{
	public override string TestMethod => GetString((a, b) =>
	{
		a ??= b;
		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create("""
			a ??= "world";

			return a;
			""", "hello", "world")
	];
}