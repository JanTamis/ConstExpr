using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class FibonacciTest() : BaseTest<Func<int, long>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return 0;
		}

		if (n == 1)
		{
			return 1;
		}

		var prev = 0L;
		var curr = 1L;

		for (var i = 2; i <= n; i++)
		{
			var next = prev + curr;
			prev = curr;
			curr = next;
		}

		return curr;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 5L;", 5),
		Create("return 1L;", 1),
		Create("return 0L;", 0),
		Create("return 55L;", 10)
	];
}