using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class ELFHashTests() : BaseTest<Func<string, uint>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(str =>
	{
		uint hash = 0;
		uint x = 0;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash = (hash << 4) + (byte) str[(int) i];

			if ((x = hash & 0xF0000000) != 0)
			{
				hash ^= x >> 24;
			}

			hash &= ~x;
		}

		return hash;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var hash = 0U;
			var x = 0U;

			for (var i = 0U; i < str.Length; i++)
			{
				hash = hash * 16U + (byte)str[(int)i];

				if ((x = hash & 4026531840U) != 0U)
					hash ^= x >> 24;

				hash &= ~x;
			}

			return hash;
			""")
	];
}