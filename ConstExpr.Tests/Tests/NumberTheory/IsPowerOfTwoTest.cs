using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPowerOfTwoTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return false;
		}

		return (n & n - 1) == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			if (n <= 0)
			{
				return false;
			}

			return (n & n - 1) == 0;
			""", Unknown),
		Create("return true;", 16),
		Create("return false;", 18)
	];
}