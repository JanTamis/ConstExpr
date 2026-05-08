using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

/// <summary>string.Format with two placeholders is rewritten to an interpolated string.</summary>
[InheritsTests]
public class StringFormatTwoArgsTest() : BaseTest<Func<string, string, string>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((first, last) => string.Format("{0} {1}", first, last));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return $\"{first} {last}\""),
		Create("return \"John Doe\";", "John", "Doe"),
	];
}