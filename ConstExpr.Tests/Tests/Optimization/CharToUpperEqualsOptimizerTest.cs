using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

[InheritsTests]
public class CharToUpperEqualsOptimizerTest() : BaseTest<Func<char, char, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((left, right) =>
	{
		return char.ToUpper(left) == char.ToUpper(right);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown),
		Create("return true;", 'a', 'A'),
		Create("return false;", 'a', 'B'),
	];
}