using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class IsSortedTest : BaseTestWithRandomValues<Func<int[], bool>>
{
	public override string TestMethod => GetString(numbers =>
	{
		if (numbers.Length <= 1)
		{
			return true;
		}

		for (var i = 1; i < numbers.Length; i++)
		{
			if (numbers[i] < numbers[i - 1])
			{
				return false;
			}
		}

		return true;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(numbers =>
		{
			ref var numbersRef = ref MemoryMarshal.GetArrayDataReference(numbers);

			var numbersLength = numbers.Length;

			if (numbersLength <= 1)
				return true;

			for (var i = 1; i < numbersLength; i++)
			{
				if (Unsafe.Add(ref numbersRef, i) < Unsafe.Add(ref numbersRef, i - 1))
					return false;
			}

			return true;
		}),
	];
}