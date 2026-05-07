using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringLengthTest() : BaseTest<Func<string?, int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(s =>
	{
		if (s is null)
		{
			return -1;
		}

		return s.Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return s?.Length ?? -1;"),
		Create("return 0;", ""),
		Create("return 11;", "hello world"),
		Create("return -1;", (string?) null)
	];
}