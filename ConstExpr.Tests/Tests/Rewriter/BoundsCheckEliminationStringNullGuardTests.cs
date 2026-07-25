using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The reference is hoisted above a null guard, so the entry point must not dereference.
///   <c>AsSpan</c> maps null to an empty span and only computes an address;
///   <c>string.GetPinnableReference()</c> would throw here instead of letting the guard return, which
///   is why the pass does not use it.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationStringNullGuardTests : BaseTest<Func<string?, int>>
{
	public override string TestMethod => GetString(text =>
	{
		if (text is null)
		{
			return 0;
		}

		return text[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// The guard itself is collapsed to a ternary by an always-on pass; what matters here is that
		// the hoisted reference sits above it and does not fault on a null string.
		Create(text =>
		{
			ref var textRef = ref MemoryMarshal.GetReference(text.AsSpan());

			return text == null ? 0 : textRef;
		}, [ Unknown ])
	];
}