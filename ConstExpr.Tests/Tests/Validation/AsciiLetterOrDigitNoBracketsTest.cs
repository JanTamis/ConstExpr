using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class AsciiLetterOrDigitNoBracketsTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	// ReSharper disable ArrangeRedundantParentheses
	public override string TestMethod => GetString(c =>
		c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(c => Char.IsAsciiLetterOrDigit(c)),
		Create(_ => true, [ '7' ]),
		Create(_ => true, [ 'x' ]),
		Create(_ => true, [ 'X' ]),
		Create(_ => false, [ '@' ]),
	];
}