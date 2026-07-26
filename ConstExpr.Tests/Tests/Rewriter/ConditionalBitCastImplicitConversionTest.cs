using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x ? 1 : 0 rewrites to Unsafe.BitCast&lt;bool, byte&gt;(x). The outer cast to the conditional's
///   target type must be dropped when byte converts to that type implicitly (int here), matching what
///   the compiler would already accept without a cast.
/// </summary>
[InheritsTests]
public class ConditionalBitCastImplicitWideningToIntTest : BaseTest<Func<double, double, int>>
{
	public override string TestMethod => GetString((x, y) => x < y ? 1 : 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => Unsafe.BitCast<bool, byte>(x < y), [ Unknown, Unknown ])
	];
}

/// <summary>
///   Same as <see cref="ConditionalBitCastImplicitWideningToIntTest" />, but for a target type (long)
///   reachable only via byte's implicit numeric widening, not the identity byte case.
/// </summary>
[InheritsTests]
public class ConditionalBitCastImplicitWideningToLongTest : BaseTest<Func<double, double, long>>
{
	public override string TestMethod => GetString((x, y) => x < y ? 1L : 0L);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => Unsafe.BitCast<bool, byte>(x < y), [ Unknown, Unknown ])
	];
}

/// <summary>
///   sbyte is NOT in byte's implicit-conversion set (only an explicit conversion exists), so the outer
///   cast must be kept here even though it is dropped for int/long/etc.
/// </summary>
[InheritsTests]
public class ConditionalBitCastKeepsCastForSByteTest : BaseTest<Func<double, double, sbyte>>
{
	public override string TestMethod => GetString((x, y) => x < y ? (sbyte) 1 : (sbyte) 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => (sbyte) Unsafe.BitCast<bool, byte>(x < y), [ Unknown, Unknown ])
	];
}

/// <summary>
///   Same int-widening shape as <see cref="ConditionalBitCastImplicitWideningToIntTest" />, but as a call
///   argument instead of a direct return. The cast must stay here even though int is implicitly
///   reachable from byte: a bare byte-typed argument can bind to a different overload (or generic
///   inference result) than the original int-typed expression did, so eliding it would silently change
///   which method gets called at a real (overloaded) call site. Confirmed via a standalone repro:
///   Overloads.SomeMethod(cond ? 1 : 0) and Overloads.SomeMethod((int) Unsafe.BitCast&lt;bool, byte&gt;(cond))
///   both call SomeMethod(int); Overloads.SomeMethod(Unsafe.BitCast&lt;bool, byte&gt;(cond)) calls
///   SomeMethod(byte) instead.
/// </summary>
[InheritsTests]
public class ConditionalBitCastKeepsCastInArgumentPositionTest : BaseTest<Func<double, double, int>>
{
	public override string TestMethod => """
		int TestMethod(double x, double y)
		{
			return Identity(x < y ? 1 : 0);
		}

		int Identity(int value) => value;
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Identity((int) Unsafe.BitCast<bool, byte>(x < y));", Unknown, Unknown)
	];
}

/// <summary>
///   The conditional sits directly as an operand of <c>+</c> between two primitive numeric types in the
///   original source. Two primitives can only ever combine via a built-in operator (C# requires a
///   user-defined operand type to overload an operator at all), so <c>RedundantBitCastElisionRewriter</c>
///   can drop the cast here without any risk of silently rebinding to a different overload.
/// </summary>
[InheritsTests]
public class ConditionalBitCastImplicitWideningInBinaryOperandTest : BaseTest<Func<double, double, int>>
{
	public override string TestMethod => GetString((x, y) => (x < y ? 1 : 0) + 5);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// The source's own parens around the conditional survive cast elision (harmless — only the
		// now-redundant cast is stripped, not the surrounding parenthesization).
		Create("return (Unsafe.BitCast<bool, byte>(x < y)) + 5;", Unknown, Unknown)
	];
}

/// <summary>
///   The scenario that motivated <c>RedundantBitCastElisionRewriter</c>: <c>a</c> is declared as
///   a `var ? 1 : 0` result (unsafe position — <c>ConditionalExpressionOptimizer</c> correctly
///   keeps the cast there, since it has no way to know yet where <c>a</c> will end up), then read exactly
///   once, so the rewriter's single-use local-variable inliner substitutes it directly into
///   <c>return a + 5;</c> — a position that IS now safe, but only decidable once the tree has stopped
///   moving, which is exactly why cast elision is a separate final pass rather than a decision made at
///   BitCast-creation time.
/// </summary>
[InheritsTests]
public class ConditionalBitCastElidesCastAfterSingleUseInliningTest : BaseTest<Func<double, double, int>>
{
	public override string TestMethod => """
		int TestMethod(double x, double y)
		{
			var a = x < y ? 1 : 0;
			return a + 5;
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Unsafe.BitCast<bool, byte>(x < y) + 5;", Unknown, Unknown)
	];
}

/// <summary>
///   The other operand of the BitCast's binary parent is itself a synthetic-looking expression: a ternary
///   whose <c>throw</c> branch carries no type. A `throw` branch can never make the ternary non-numeric —
///   only the surviving branch's type matters, mirroring how real C# types <c>cond ? n : throw ex</c>.
/// </summary>
[InheritsTests]
public class ConditionalBitCastElidesCastNextToTernaryWithThrowTest : BaseTest<Func<bool, int, int>>
{
	public override string TestMethod => """
		int TestMethod(bool flag, int n)
		{
			return (flag ? n : throw new Exception()) + (flag ? 1 : 0);
		}
		""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (flag ? n : throw new Exception()) + (Unsafe.BitCast<bool, byte>(flag));", Unknown, Unknown)
	];
}