using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DivideByConstantMagicTest() : BaseTest<Func<uint, uint>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => x / 3u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unknown divisor operand: x / 3u is strength-reduced to a multiply-high + shift.
		Create("return (uint)((ulong)x * 2863311531UL >> 33);", Unknown),
		// Known operand still constant-folds (handled by DivideConstantFoldingStrategy).
		Create(_ => 4u, [ 12u ]),
		Create(_ => 0u, [ 2u ]),
		Create(_ => 7u, [ 21u ])
	];
}