using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ConcatenateTest() : BaseTest<Func<string, string, string>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => a + b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => "helloworld", [ "hello", "world" ]),
		Create((_, _) => "test", [ "test", System.String.Empty ]),
		Create((_, _) => "", [ System.String.Empty, System.String.Empty ])
	];
}