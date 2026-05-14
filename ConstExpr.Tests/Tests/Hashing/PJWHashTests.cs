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
			var hash = 0U;
			var test = 0U;

			for (var i = 0U; i < str.Length; i++)
			{
				hash = hash * 16U + (byte)str[(int)i];

				if ((test = hash & 4026531840U) != 0U)
					hash = (hash ^ test >> 24) & 268435455U;
			}

			return hash;
			""")
	];
}