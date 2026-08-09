using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class RemoveWhitespaceTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(input =>
	{
		if (System.String.IsNullOrEmpty(input))
			return input;

		var result = new char[input.Length];
		var index = 0;

		foreach (var c in input)
		{
			if (!Char.IsWhiteSpace(c))
				result[index++] = c;
		}

		return new string(result, 0, index);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(input =>
		{
			var inputLength = input.Length;

			if (inputLength == 0)
				return input;

			var result = new char[inputLength];
			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);

			var index = 0;

			foreach (var c in input)
			{
				if (!Char.IsWhiteSpace(c))
					Unsafe.Add(ref resultRef, index++) = c;
			}

			return new string(result, 0, index);
		}),
		CreateFolded("Hello World"),
		CreateFolded("  Test  String  "),
		CreateFolded("   "),
		CreateFolded("abc")
	];
}