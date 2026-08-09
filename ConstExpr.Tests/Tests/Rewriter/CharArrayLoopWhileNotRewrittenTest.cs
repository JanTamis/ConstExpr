using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Guard: the char[]-loop pattern only matches a canonical <c>for</c> loop. A <c>while</c> loop
///   over the same array must NOT be rewritten to <c>string.Create</c>.
/// </summary>
[InheritsTests]
public class CharArrayLoopWhileNotRewrittenTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(input =>
	{
		var result = input.ToCharArray();
		var i = 0;

		while (i < result.Length)
		{
			result[i] = Char.ToUpper(result[i]);
			i++;
		}

		return new string(result);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(input =>
		{
			var result = input.ToCharArray();
			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);

			var i = 0;

			while (i < result.Length)
			{
				Unsafe.Add(ref resultRef, i) = Char.ToUpper(Unsafe.Add(ref resultRef, i));
				i++;
			}

			return new string(result);
		}),
		CreateFolded("hello")
	];
}