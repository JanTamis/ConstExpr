using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Count() >= 1 → source.Any() and Count(predicate) >= 1 → source.Any(predicate).
/// </summary>
[InheritsTests]
public class LinqCountGreaterOrEqualOneToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() >= 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x.Any();"),
		Create("return false;", Enumerable.Empty<int>()),
		Create("return true;", new[] { 1 }),
		Create("return true;", new[] { 1, 2, 3 })
	];
}

[InheritsTests]
public class LinqCountGreaterOrEqualOneWithPredicateToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count(v => v > 5) >= 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x.Any(v => v > 5);"),
		Create("return false;", Enumerable.Empty<int>()),
		Create("return false;", new[] { 1, 2, 3 }),
		Create("return true;", new[] { 6 })
	];
}

[InheritsTests]
public class LinqCountGreaterOrEqualOneNotOptimizedTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() >= 2;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return false;", Enumerable.Empty<int>()),
		Create("return false;", new[] { 42 }),
		Create("return true;", new[] { 1, 2 })
	];
}