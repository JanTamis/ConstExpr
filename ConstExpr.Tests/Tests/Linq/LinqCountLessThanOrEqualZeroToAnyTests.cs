using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Count() &lt;= 0 → !(source.Any()) and Count(predicate) &lt;= 0 → !(source.Any(predicate)).
/// </summary>
[InheritsTests]
public class LinqCountLessThanOrEqualZeroToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() <= 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return !x.Any();"),
		Create("return true;", Enumerable.Empty<int>()),
		Create("return false;", new[] { 1, 2, 3 })
	];
}

[InheritsTests]
public class LinqCountLessThanOrEqualZeroWithPredicateToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count(v => v > 5) <= 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return !x.Any(v => v > 5);"),
		Create("return true;", Enumerable.Empty<int>()),
		Create("return true;", new[] { 1, 2, 3 }),
		Create("return false;", new[] { 1, 6, 3 })
	];
}

[InheritsTests]
public class LinqCountLessThanOrEqualZeroNotOptimizedTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() <= 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return true;", Enumerable.Empty<int>()),
		Create("return true;", new[] { 42 }),
		Create("return false;", new[] { 1, 2 })
	];
}