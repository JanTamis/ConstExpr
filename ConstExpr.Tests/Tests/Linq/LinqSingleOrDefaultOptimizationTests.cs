namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for SingleOrDefault() optimization - verify Where fusion and chain optimization
/// </summary>
[InheritsTests]
public class LinqSingleOrDefaultOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(predicate).SingleOrDefault() => SingleOrDefault(predicate)
		var a = x.Where(v => v == 3).SingleOrDefault();

		// AsEnumerable().ToList().Where(predicate).SingleOrDefault() => SingleOrDefault(predicate)
		var b = x.AsEnumerable().ToList().Where(v => v == 99).SingleOrDefault();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = SingleOrDefault_8gQvSw(x);
			var b = SingleOrDefault_8gQvSw(x);
			
			return a + b;
			""", Unknown),
		Create("return 3;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new[] { 1, 2, 4, 5 }),
	];
}
