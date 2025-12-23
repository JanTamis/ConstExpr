using System.Linq.Expressions;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class AbsoluteValueTest() : BaseTest<Func<int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0)
		{
			return -n;
		}

		return n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 42;", -42),
		Create("return 10;", 10),
		Create("return 0;", 0)
	];
}