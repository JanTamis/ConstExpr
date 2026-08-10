using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class BPHashTests : BaseTestWithRandomValues<Func<string, uint>>
{
	public override string TestMethod => GetString(str =>
	{
		uint hash = 0;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash = hash << 7 ^ (byte) str[(int) i];
		}

		return hash;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(str =>
		{
			ref var strRef = ref MemoryMarshal.GetReference(str.AsSpan());
			var hash = 0U;

			for (var i = 0U; i < str.Length; i++)
				hash = hash << 7 ^ (byte) Unsafe.Add(ref strRef, (int) i);

			return hash;
		})
	];
}