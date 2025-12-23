using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumRangeTest() : BaseTest<Func<int, int, long>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((start, end) =>
	{
		if (start > end)
		{
			var temp = start;
			start = end;
			end = temp;
		}

		var n = end - start + 1;
		return (long) n * (start + end) / 2L;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 55L;", 1, 10),
		Create("return 5050L;", 1, 100),
		Create("return 25L;", 3, 7)
	];
}