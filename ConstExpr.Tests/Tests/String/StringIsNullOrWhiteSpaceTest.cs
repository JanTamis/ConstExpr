using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIsNullOrWhiteSpaceTest() : BaseTest<Func<string, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s => System.String.IsNullOrWhiteSpace(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => string.IsNullOrWhiteSpace(s)),
		Create(_ => true, [ System.String.Empty ]),
		Create(_ => true, [ "   " ]),
		Create(_ => false, [ "hello" ]),
		Create(_ => false, [ " x " ])
	];
}