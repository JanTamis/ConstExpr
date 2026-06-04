using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToUpperCaseTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s => s.ToUpper());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => "HELLO", [ "hello" ]),
		Create(_ => "WORLD123", [ "WoRlD123" ]),
		Create(_ => "", [ "" ])
	];
}