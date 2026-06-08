using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>When replacement differs, no optimisation is applied.</summary>
[InheritsTests]
public class StringReplaceWithDifferentArgsTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s => s.Replace("a", "b"));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s.Replace('a', 'b')),
		Create(_ => "hello", [ "hello" ]),
		Create(_ => "bbnbnb", [ "banana" ]),
	];
}