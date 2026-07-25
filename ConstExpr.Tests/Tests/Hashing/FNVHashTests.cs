using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class FNVHashTests : BaseTest<Func<string, uint>>
{
	public override string TestMethod => GetString(str =>
	{
		const uint fnv_prime = 0x811C9DC5;
		uint hash = 0;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash *= fnv_prime;
			hash ^= (byte) str[(int) i];
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
			{
				hash *= 2166136261U;
				hash ^= (byte) Unsafe.Add(ref strRef, (int) i);
			}

			return hash;
		})
	];
}