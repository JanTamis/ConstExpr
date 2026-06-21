using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Hashing;

[InheritsTests]
public class DJBHashTests() : BaseTest<Func<string, uint>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(str =>
	{
		uint hash = 5381;
		uint i = 0;

		for (i = 0; i < str.Length; i++)
		{
			hash = (hash << 5) + hash + (byte)str[(int)i];
		}

		return hash;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(str =>
		{
			var hash = 5381U;

			for (var i = 0U; i < str.Length; i++)
			{
				hash = hash * 33U + (byte)str[(int)i];
			}

			return hash;
		})
	];
}