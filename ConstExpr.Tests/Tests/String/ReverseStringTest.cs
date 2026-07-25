using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ReverseStringTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(s =>
	{
		var chars = s.ToCharArray();
		var left = 0;
		var right = chars.Length - 1;

		while (left < right)
		{
			var temp = chars[left];
			chars[left] = chars[right];
			chars[right] = temp;

			left++;
			right--;
		}

		return new string(chars);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s =>
		{
			var chars = s.ToCharArray();
			ref var charsRef = ref MemoryMarshal.GetArrayDataReference(chars);
			var left = 0;
			var right = chars.Length - 1;

			while (left < right)
			{
				(Unsafe.Add(ref charsRef, left), Unsafe.Add(ref charsRef, right)) = (Unsafe.Add(ref charsRef, right), Unsafe.Add(ref charsRef, left));
				left++;
				right--;
			}

			return new string(chars);
		}),
		Create(_ => "olleh", [ "hello" ]),
		Create(_ => "", [ System.String.Empty ]),
		Create(_ => "a", [ "a" ])
	];
}