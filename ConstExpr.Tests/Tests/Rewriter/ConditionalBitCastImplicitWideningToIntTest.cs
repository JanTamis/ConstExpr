using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x ? 1 : 0 rewrites to Unsafe.BitCast&lt;bool, byte&gt;(x). The outer cast to the conditional's
///   target type must be dropped when byte converts to that type implicitly (int here), matching what
///   the compiler would already accept without a cast.
/// </summary>
[InheritsTests]
public class ConditionalBitCastImplicitWideningToIntTest : BaseTestWithRandomValues<Func<double, double, int>>
{
	public override string TestMethod => GetString((x, y) => x < y ? 1 : 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => Unsafe.BitCast<bool, byte>(x < y))
	];
}