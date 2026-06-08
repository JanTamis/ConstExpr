using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ConcatenateTest() : BaseTest<Func<string, string, string>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => a + b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => "helloworld", [ "hello", "world" ]),
		Create((_, _) => "test", [ "test", "" ]),
		Create((_, _) => "", [ "", "" ])
	];
}