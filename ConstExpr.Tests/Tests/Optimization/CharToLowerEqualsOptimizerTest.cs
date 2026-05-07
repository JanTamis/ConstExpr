using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CharToLowerEqualsOptimizerTest() : BaseTest<Func<char, char, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((left, right) =>
	{
		return char.ToLower(left) == char.ToLower(right);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown),
		Create("return true;", 'A', 'a'),
		Create("return false;", 'A', 'b'),
	];
}