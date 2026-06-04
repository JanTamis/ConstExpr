using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Count() > 0 → source.Any() and Count(predicate) > 0 → source.Any(predicate).
/// </summary>
[InheritsTests]
public class LinqCountGreaterThanZeroToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() > 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x.Any();"),
		Create("return false;", Enumerable.Empty<int>()),
		Create("return true;", new[] { 1, 2, 3 })
	];
}

[InheritsTests]
public class LinqCountGreaterThanZeroWithPredicateToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count(v => v > 5) > 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x.Any(v => v > 5);"),
		Create("return false;", Enumerable.Empty<int>()),
		Create("return false;", new[] { 1, 2, 3 }),
		Create("return true;", new[] { 1, 6, 3 })
	];
}

[InheritsTests]
public class LinqCountGreaterThanZeroNotOptimizedTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() > 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return false;", Enumerable.Empty<int>()),
		Create("return false;", new[] { 42 }),
		Create("return true;", new[] { 1, 2 })
	];
}