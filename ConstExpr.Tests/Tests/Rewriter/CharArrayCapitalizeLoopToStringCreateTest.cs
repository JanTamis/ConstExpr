using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A <c>ToCharArray()</c> declaration followed by a canonical <c>for</c> loop that mutates the
///   array in place, followed by <c>new string(...)</c>, is rewritten to <c>string.Create</c>.
/// </summary>
[InheritsTests]
public class CharArrayCapitalizeLoopToStringCreateTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(input =>
	{
		var result = input.ToCharArray();
		var capitalizeNext = true;

		for (var i = 0; i < result.Length; i++)
		{
			var c = result[i];

			if (Char.IsWhiteSpace(c))
			{
				capitalizeNext = true;
			}
			else if (capitalizeNext)
			{
				result[i] = Char.ToUpper(c);
				capitalizeNext = false;
			}
		}

		return new string(result);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(input =>
		{
			var result = input.ToCharArray();
			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);
			var capitalizeNext = true;

			for (var i = 0; i < result.Length; i++)
			{
				var c = Unsafe.Add(ref resultRef, i);

				if (Char.IsWhiteSpace(c))
				{
					capitalizeNext = true;
				}
				else if (capitalizeNext)
				{
					Unsafe.Add(ref resultRef, i) = Char.ToUpper(c);
					capitalizeNext = false;
				}
			}

			return new string(result);
		}),
		CreateFolded("hello world"),
		CreateFolded(System.String.Empty),
		CreateFolded("Already Capitalized")
	];
}