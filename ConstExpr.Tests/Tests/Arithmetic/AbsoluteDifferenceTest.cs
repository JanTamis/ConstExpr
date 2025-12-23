using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class AbsoluteDifferenceTest() : BaseTest<Func<int, int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((a, b) =>
	{
		var diff = a - b;

		return diff < 0 ? -diff : diff;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 5;", 10, 5),
		Create("return 30;", -10, 20),
		Create("return 0;", 42, 42)
	];
}