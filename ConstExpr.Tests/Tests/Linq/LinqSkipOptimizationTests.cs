namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Skip() optimization - verify Skip(0) removal
/// </summary>
[InheritsTests]
public class LinqSkipOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Skip(0) => source
		var a = x.Skip(0).Count();

		var b = x.Skip(1).Skip(3).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			var xLength = x.Length;

			return xLength + Int32.Max(0, xLength - 4);
		}),
		CreateFolded(new[] { 1, 2, 3 }),
		CreateFolded(System.Array.Empty<int>())
	];
}