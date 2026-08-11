using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceAssignmentFlagOffTest() : BaseTestWithRandomValues<Func<string, string, string>>(optimizations: OptimizationFlags.All & ~OptimizationFlags.UseNullableAnnotations)
{
	public override string TestMethod => GetString((a, b) =>
	{
		a ??= b;
		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		// UseNullableAnnotations only gates ANNOTATION-derived non-null proofs (see its doc comment) -
		// a is KNOWN here, not merely annotated, so folding still happens with the flag off.
		CreateFolded("hello", "world")
	];
}