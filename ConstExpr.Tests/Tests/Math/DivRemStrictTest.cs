using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>
///   <c>Math.DivRem(a, b)</c> becomes the tuple <c>(a / b, a % b)</c> even under
///   <see cref="FastMathFlags.Strict" /> — same quotient, remainder and throw behaviour for every
///   integer input, so <c>DivRemFunctionOptimizer</c> marks itself <c>RequiredFlags = [Strict]</c>.
///   Unknown-argument case only (the string comparison never runs the body, so <c>b == 0</c> is a
///   non-issue here).
/// </summary>
[InheritsTests]
public class DivRemStrictTest() : BaseTest<Func<int, int, (int, int)>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString((a, b) => System.Math.DivRem(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (a / b, a % b);")
	];
}