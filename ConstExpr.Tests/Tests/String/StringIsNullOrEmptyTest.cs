using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIsNullOrEmptyTest() : BaseTest<Func<string, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(s => System.String.IsNullOrEmpty(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => System.String.IsNullOrEmpty(s)),
		Create(_ => true, [ System.String.Empty ]),
		Create(_ => false, [ "hello" ]),
		Create(_ => false, [ "x" ])
	];
}