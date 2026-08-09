namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class AverageTest : BaseTestWithRandomValues<Func<int[], double>>
{
	public override string TestMethod => GetString(numbers =>
	{
		if (numbers.Length == 0)
		{
			return 0D;
		}

		var sum = 0;

		foreach (var num in numbers)
		{
			sum += num;
		}

		return (double) sum / numbers.Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(numbers =>
		{
			var numbersLength = numbers.Length;

			if (numbersLength == 0)
				return 0.0;

			var sum = 0;

			foreach (var num in numbers)
				sum += num;

			return (double) sum / numbersLength;
		}),
	];
}