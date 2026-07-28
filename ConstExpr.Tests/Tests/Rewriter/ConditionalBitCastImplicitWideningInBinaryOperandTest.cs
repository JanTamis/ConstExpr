using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Rewriter;

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
		Create((x, y) => Unsafe.BitCast<bool, byte>(x < y) + 5)
	];
}