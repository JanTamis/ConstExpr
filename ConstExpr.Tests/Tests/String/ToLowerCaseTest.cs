using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToLowerCaseTest() : BaseTest<Func<string, string>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(s => s.ToLower());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => "hello", [ "HELLO" ]),
		Create(_ => "world123", [ "WoRlD123" ]),
		Create(_ => "", [ System.String.Empty ])
	];
}