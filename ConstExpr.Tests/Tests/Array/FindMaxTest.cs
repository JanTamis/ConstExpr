using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class FindMaxTest : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(numbers =>
	{
		if (numbers.Length == 0)
		{
			return 0;
		}

		var max = numbers[0];

		for (var i = 1; i < numbers.Length; i++)
		{
			if (numbers[i] > max)
			{
				max = numbers[i];
			}
		}

		return max;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(numbers =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);

			var numbersLength = numbers.Length;

			if (numbersLength == 0)
				return 0;

			var max = numbersRef;

			for (var i = 1; i < numbersLength; i++)
			{
				var item = Unsafe.Add(ref numbersRef, i);

				if (item > max)
					max = item;
			}

			return max;
		}),
		Create(_ => 50, [ new[] { 10, 20, 50, 30 } ]),
		Create(_ => 100, [ new[] { 5, 15, 25, 100, 50 } ]),
		Create(_ => -5, [ new[] { -10, -20, -5, -30 } ])
	];
}