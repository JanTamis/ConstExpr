using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class PJWHashTests() : BaseTest<Func<string, uint>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(str =>
	{
		const uint BitsInUnsignedInt = (uint) (sizeof(uint) * 8);
		const uint ThreeQuarters = (uint) ((BitsInUnsignedInt * 3) / 4);
		const uint OneEighth = (uint) (BitsInUnsignedInt / 8);
		const uint HighBits = (uint) (0xFFFFFFFF) << (int) (BitsInUnsignedInt - OneEighth);
		uint hash = 0;
		uint test = 0;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash = (hash << (int) OneEighth) + ((byte) str[(int) i]);

			if ((test = hash & HighBits) != 0)
			{
				hash = ((hash ^ (test >> (int) ThreeQuarters)) & (~HighBits));
			}
		}

		return hash;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var hash = 2863311530U;

			for (var i = 0U; i < str.Length; i++)
			{
				hash ^= UInt32.IsEvenInteger(i) ? hash << 7 ^ (byte) str[(int) i] * (hash >> 3) : ~((hash << 11) + ((byte) str[(int) i] ^ hash >> 5));
			}

			return hash;
			""")
	];
}