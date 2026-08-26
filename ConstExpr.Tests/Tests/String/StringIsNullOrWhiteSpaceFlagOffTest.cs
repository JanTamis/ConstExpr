using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>
///   Contrasts with StringIsNullOrEmptyFlagOffTest: unlike IsNullOrEmpty, IsNullOrWhiteSpace no longer
///   uses UseNullableAnnotations to gate *whether* it folds - the hybrid helper handles null itself, so
///   the fold happens regardless. With the flag off, CanBeNull falls back to true (nullability
///   unproven), so the call site is still the null-checking IsNullOrWhiteSpaceFast(s), not the
///   unfolded original call.
/// </summary>
[InheritsTests]
public class StringIsNullOrWhiteSpaceFlagOffTest() : BaseTestWithRandomValues<Func<string, bool>>(optimizations: OptimizationFlags.All & ~OptimizationFlags.UseNullableAnnotations)
{
	public override string TestMethod => GetString(s => System.String.IsNullOrWhiteSpace(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return IsNullOrWhiteSpaceFast(s);")
	];
}