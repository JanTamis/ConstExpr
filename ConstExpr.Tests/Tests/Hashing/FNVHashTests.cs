using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class FNVHashTests() : BaseTest<Func<string, uint>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(str =>
	{
		const uint fnv_prime = 0x811C9DC5;
		uint hash = 0;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash *= fnv_prime;
			hash ^= (byte)str[(int)i];
		}

		return hash;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(str =>
		{
			var hash = 0U;

			for (var i = 0U; i < str.Length; i++)
			{
				hash *= 2166136261U;
				hash ^= (byte)str[(int)i];
			}

			return hash;
		})
	];
}