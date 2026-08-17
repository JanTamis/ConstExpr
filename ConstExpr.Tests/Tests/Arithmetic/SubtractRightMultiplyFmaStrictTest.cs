using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SubtractRightMultiplyFmaStrictTest() : BaseTestWithRandomValues<Func<double, double, double>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString((x, y) => x - y * x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown)
	];
}