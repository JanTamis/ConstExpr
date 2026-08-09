namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumRangeTest : BaseTest<Func<int, int, long>>
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((start, end) =>
		{
			if (start > end)
				(start, end) = (end, start);

			return (long) (end - start + 1) * (start + end) / 2L;
		}),
		CreateFolded(1, 10),
		CreateFolded(1, 100),
		CreateFolded(3, 7)
	];
}