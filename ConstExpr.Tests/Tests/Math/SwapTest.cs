using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SwapTest() : BaseTest<Func<int, int, (int, int)>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((a, b) =>
	{
		var temp = a;
		a = b;
		b = temp;

		return (a, b);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return (20, 10);", 10, 20),
		Create("return (0, 42);", 42, 0),
		Create("return (-5, 5);", 5, -5)
	];
}