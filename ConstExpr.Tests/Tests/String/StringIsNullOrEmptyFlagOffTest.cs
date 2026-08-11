using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>
///   Proves the UseNullableAnnotations gate actually works: with the flag off, String.IsNullOrEmpty(s)
///   must survive unfolded even though s is non-nullable (provably non-null).
/// </summary>
[InheritsTests]
public class StringIsNullOrEmptyFlagOffTest() : BaseTestWithRandomValues<Func<string, bool>>(optimizations: OptimizationFlags.All & ~OptimizationFlags.UseNullableAnnotations)
{
	public override string TestMethod => GetString(s => System.String.IsNullOrEmpty(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}