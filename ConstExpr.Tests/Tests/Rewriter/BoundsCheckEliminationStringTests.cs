using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A string is viewed as a <c>ReadOnlySpan&lt;char&gt;</c> via <c>AsSpan</c>. Reads only — a string
///   has no indexer setter — and <c>.Length</c> stays untouched.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationStringTests : BaseTestWithRandomValues<Func<string, int>>
{
	public override string TestMethod => GetString(text =>
	{
		var sum = text[0];

		for (var i = 1; i < text.Length; i++)
		{
			sum += text[i];
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(text =>
		{
			ref var textRef = ref MemoryMarshal.GetReference(text.AsSpan());
			var sum = textRef;

			for (var i = 1; i < text.Length; i++)
			{
				sum += Unsafe.Add(ref textRef, i);
			}

			return sum;
		})
	];
}