using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIsNullOrEmptyTest() : BaseTest<Func<string, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s => string.IsNullOrEmpty(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => true, [ "" ]),
		Create(_ => false, [ "hello" ]),
		Create(_ => false, [ "x" ]),
	];
}