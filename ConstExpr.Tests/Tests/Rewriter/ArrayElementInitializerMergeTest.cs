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
		var result = new int[numbers.Length];

		for (var i = 0; i < result.Length; i++)
		{
			result[i] = numbers[(positions + i) % 6];
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
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
		}, [ new[] { 1, 2, 3, 4, 5, 6 }, Unknown ])
	];
}