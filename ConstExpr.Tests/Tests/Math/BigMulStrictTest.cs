using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>
///   <c>Math.BigMul(int, int)</c> becomes <c>(long)a * (long)b</c> even under
///   <see cref="FastMathFlags.Strict" />: the widening multiply is bit-for-bit exact, so
///   <c>BigMulFunctionOptimizer</c> opts in with <c>RequiredFlags = [Strict]</c> rather than the
///   default <c>[NoNaN]</c> gate.
/// </summary>
[InheritsTests]
public class BigMulStrictTest() : BaseTestWithRandomValues<Func<int, int, long>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString((a, b) => System.Math.BigMul(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (long) a * (long) b;")
	];
}