using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class DEKHashTests : BaseTest<Func<string, uint>>
{
	public override string TestMethod => GetString(str =>
	{
		var hash = (uint) str.Length;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash = hash << 5 ^ hash >> 27 ^ (byte) str[(int) i];
		}

		return hash;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(str =>
		{
			ref var strRef = ref MemoryMarshal.GetReference(str.AsSpan());

			var strLength = str.Length;
			var hash = (uint) strLength;

			for (var i = 0U; i < strLength; i++)
				hash = hash << 5 ^ hash >> 27 ^ (byte) Unsafe.Add(ref strRef, (int) i);

			return hash;
		})
	];
}