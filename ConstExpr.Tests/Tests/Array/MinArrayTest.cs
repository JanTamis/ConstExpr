namespace ConstExpr.Tests.Array;

[InheritsTests]
public class MinArrayTest : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(values =>
	{
		if (values.Length == 0)
		{
			return Int32.MaxValue;
		}

		var min = Int32.MaxValue;

		foreach (var v in values)
		{
			if (v < min)
			{
				min = v;
			}
		}

		return min;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(values =>
		{
			if (values.Length == 0)
				return Int32.MaxValue;

			var min = Int32.MaxValue;

			foreach (var v in values)
				min = Int32.Min(v, min);

			return min;
		}),
	];
}