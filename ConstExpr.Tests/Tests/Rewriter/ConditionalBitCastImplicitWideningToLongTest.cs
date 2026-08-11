using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Same as <see cref="ConditionalBitCastImplicitWideningToIntTest" />, but for a target type (long)
///   reachable only via byte's implicit numeric widening, not the identity byte case.
/// </summary>
[InheritsTests]
public class ConditionalBitCastImplicitWideningToLongTest : BaseTestWithRandomValues<Func<double, double, long>>
{
	public override string TestMethod => GetString((x, y) => x < y ? 1L : 0L);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => Unsafe.BitCast<bool, byte>(x < y))
	];
}