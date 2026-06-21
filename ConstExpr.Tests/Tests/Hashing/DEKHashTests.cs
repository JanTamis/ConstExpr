using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class DEKHashTests() : BaseTest<Func<string, uint>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(str =>
	{
		var hash = (uint)str.Length;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash = hash << 5 ^ hash >> 27 ^ (byte)str[(int)i];
		}

		return hash;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(str =>
		{
			var hash = (uint)str.Length;

			for (var i = 0U; i < str.Length; i++)
			{
				hash = hash << 5 ^ hash >> 27 ^ (byte)str[(int)i];
			}

			return hash;
		})
	];
}