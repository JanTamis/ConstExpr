namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for TakeWhile() optimization - verify constant predicate handling
/// </summary>
[InheritsTests]
public class LinqTakeWhileOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// TakeWhile(v => true) => source (take everything)
		var a = x.TakeWhile(v => true).Count();

		// TakeWhile(v => false) => Empty (take nothing)
		var b = x.TakeWhile(v => false).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Length;

			return a;
			""", Unknown),
		Create("return 3;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

