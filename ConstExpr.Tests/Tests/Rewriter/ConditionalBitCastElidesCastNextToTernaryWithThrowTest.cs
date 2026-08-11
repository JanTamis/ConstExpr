using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The other operand of the BitCast's binary parent is itself a synthetic-looking expression: a ternary
///   whose <c>throw</c> branch carries no type. A `throw` branch can never make the ternary non-numeric —
///   only the surviving branch's type matters, mirroring how real C# types <c>cond ? n : throw ex</c>.
/// </summary>
[InheritsTests]
public class ConditionalBitCastElidesCastNextToTernaryWithThrowTest : BaseTestWithRandomValues<Func<bool, int, int>>
{
	public override string TestMethod => GetString((flag, n) => (flag ? n : throw new Exception()) + (flag ? 1 : 0));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((flag, n) => (flag ? n : throw new Exception()) + Unsafe.BitCast<bool, byte>(flag))
	];
}