using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
/// Verifies that the ASCII char range optimizer works when no explicit parentheses are
/// written. Because &amp;&amp; has higher precedence than ||, the parser produces the same AST
/// as the parenthesized versions, so all patterns should still be recognized.
/// </summary>
[InheritsTests]
public class AsciiCharRangeNoBracketsTest() : BaseTest<Func<char, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	// No parentheses — &&-precedence groups identically to the parenthesized form.
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => Char.IsAsciiHexDigit(c)),
		Create(_ => true, [ '5' ]),
		Create(_ => true, [ 'b' ]),
		Create(_ => true, [ 'E' ]),
		Create(_ => false, [ 'z' ]),
	];
}