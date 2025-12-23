using System.Linq.Expressions;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class HypotenuseTest() : BaseTest<Func<int, int, double>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((a, b) => System.Math.Sqrt(a * a + b * b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return Double.Sqrt(a * a + b * b);", Unknown, Unknown),
		Create("return 5D;", 3, 4),
		Create("return 13D;", 5, 12),
		Create("return 10D;", 0, 10)
	];
}