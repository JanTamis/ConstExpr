namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A <c>new T[N]</c> declaration followed by N sequential constant-index assignments
///   (<c>result[0] = ...; result[1] = ...;</c>) is merged into a single array initializer
///   (<c>new T[] { ..., ... }</c>).
/// </summary>
[InheritsTests]
public class ArrayElementInitializerMergeTest : BaseTest<Func<int[], int, int[]>>
{
	public override string TestMethod => GetString((numbers, positions) =>
	{
		var result = new int[6];

		result[0] = numbers[positions % 6];
		result[1] = numbers[(positions + 1) % 6];
		result[2] = numbers[(positions + 2) % 6];
		result[3] = numbers[(positions + 3) % 6];
		result[4] = numbers[(positions + 4) % 6];
		result[5] = numbers[(positions + 5) % 6];

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numbers, positions) =>
		{
			return new[]
			{
				numbers[positions % 6],
				numbers[(positions + 1) % 6],
				numbers[(positions + 2) % 6],
				numbers[(positions + 3) % 6],
				numbers[(positions + 4) % 6],
				numbers[(positions + 5) % 6]
			};
		})
	];
}