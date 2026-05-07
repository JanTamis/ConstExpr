using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CharToUpperNotEqualsOptimizerTest() : BaseTest<Func<char, char, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((left, right) =>
	{
		return char.ToUpper(left) != char.ToUpper(right);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown),
		Create("return false;", 'a', 'A'),
		Create("return true;", 'a', 'B'),
	];
}