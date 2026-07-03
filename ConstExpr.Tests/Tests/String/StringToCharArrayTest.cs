using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringToCharArrayTest() : BaseTest<Func<string, char[]>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(s => s.ToCharArray());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => [ 'h', 'i' ], [ "hi" ]),
		Create(_ => [ 'a', 'b', 'c' ], [ "abc" ]),
		Create(_ => [ ], [ System.String.Empty ])
	];
}