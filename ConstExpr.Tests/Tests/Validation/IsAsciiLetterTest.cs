using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
///   Tests that (c >= 'a' &amp;&amp; c &lt;= 'z') || (c >= 'A' &amp;&amp; c &lt;= 'Z')
///   is collapsed into <c>Char.IsAsciiLetter(c)</c>.
/// </summary>
[InheritsTests]
public class IsAsciiLetterTest() : BaseTest<Func<char, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(c =>
		c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => Char.IsAsciiLetter(c)),
		Create(_ => true, [ 'm' ]),
		Create(_ => true, [ 'M' ]),
		Create(_ => false, [ '5' ])
	];
}