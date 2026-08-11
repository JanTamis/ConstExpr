using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   sbyte is NOT in byte's implicit-conversion set (only an explicit conversion exists), so the outer
///   cast must be kept here even though it is dropped for int/long/etc.
/// </summary>
[InheritsTests]
public class ConditionalBitCastKeepsCastForSByteTest : BaseTestWithRandomValues<Func<double, double, sbyte>>
{
	public override string TestMethod => GetString((x, y) => x < y ? (sbyte) 1 : (sbyte) 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => (sbyte) Unsafe.BitCast<bool, byte>(x < y))
	];
}