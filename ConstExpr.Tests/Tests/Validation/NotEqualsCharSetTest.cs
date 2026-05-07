using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

/// <summary>
/// Verifies that negated char-set bitmask rewrites preserve logical precedence
/// when one branch stays as an explicit inequality.
/// </summary>
[InheritsTests]
public class NotEqualsCharSetTest() : BaseTest<Func<char, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(c => c != '-' && c != ' ' && c != '+');

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (uint)(c - ' ') > 13U || (0x2801u >> c - ' ' & 1) == 0;"),
		Create("return false;", '-'),
		Create("return false;", ' '),
		Create("return false;", '+'),
		Create("return true;", 'x'),
	];
}