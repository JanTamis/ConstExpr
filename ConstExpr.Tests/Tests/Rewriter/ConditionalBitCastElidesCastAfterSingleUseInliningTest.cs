using System.Runtime.CompilerServices;

namespace ConstExpr.Tests.Rewriter;

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
public class ConditionalBitCastElidesCastAfterSingleUseInliningTest : BaseTestWithRandomValues<Func<double, double, int>>
{
	public override string TestMethod => GetString((x, y) =>
	{
		var a = x < y ? 1 : 0;

		return a + 5;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => Unsafe.BitCast<bool, byte>(x < y) + 5)
	];
}