using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class BKDRHashTests : BaseTest<Func<string, uint>>
{
	public override string TestMethod => GetString(str =>
	{
		uint seed = 131;
		uint hash = 0;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash = hash * seed + (byte) str[(int) i];
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
				hash = hash * 131U + (byte) Unsafe.Add(ref strRef, (int) i);

			return hash;
		})
	];
}