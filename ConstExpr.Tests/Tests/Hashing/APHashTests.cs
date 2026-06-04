using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class APHashTests() : BaseTest<Func<string, uint>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(str =>
	{
		var hash = 0xAAAAAAAA;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash ^= (i & 1) == 0 ? hash << 7 ^ (byte) str[(int) i] * (hash >> 3) : ~((hash << 11) + ((byte) str[(int) i] ^ hash >> 5));
		}

		return hash;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(str =>
		{
			var hash = 2863311530U;

			for (var i = 0U; i < str.Length; i++)
			{
				hash ^= UInt32.IsEvenInteger(i) ? hash << 7 ^ (byte) str[(int) i] * (hash >> 3) : ~((hash << 11) + ((byte) str[(int) i] ^ hash >> 5));
			}

			return hash;
		})
	];
}