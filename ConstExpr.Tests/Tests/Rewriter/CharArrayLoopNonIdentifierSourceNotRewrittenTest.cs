using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Guard: when the <c>ToCharArray()</c> receiver is not a bare identifier (here, a method call),
///   the char[]-loop pattern must NOT be rewritten to <c>string.Create</c> — the receiver would
///   otherwise need to be evaluated twice (once for its length, once as the state argument).
/// </summary>
[InheritsTests]
public class CharArrayLoopNonIdentifierSourceNotRewrittenTest : BaseTestWithRandomValues<Func<string, string>>
{
	public override string TestMethod => GetString(input =>
	{
		var result = input.ToUpperInvariant().ToCharArray();

		for (var i = 0; i < result.Length; i++)
		{
			if (Char.IsWhiteSpace(result[i]))
			{
				result[i] = '_';
			}
		}

		return new string(result);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(input =>
		{
			var result = input.ToUpperInvariant().ToCharArray();
			ref var resultRef = ref MemoryMarshal.GetArrayDataReference(result);

			for (var i = 0; i < result.Length; i++)
			{
				if (Char.IsWhiteSpace(Unsafe.Add(ref resultRef, i)))
					Unsafe.Add(ref resultRef, i) = '_';
			}

			return new string(result);
		}),
	];
}