using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Count() == 0 → !(source.Any()) and Count(predicate) == 0 → !(source.Any(predicate)).
///   Also covers the reversed form: 0 == Count().
///   Uses LinqOptimisationMode.None so Count() is not replaced by an unrolled helper before
///   the binary optimizer can match the x.Count() syntax.
/// </summary>
[InheritsTests]
public class LinqCountEqualsZeroToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return !x.Any();"),
		Create("return true;", Enumerable.Empty<int>()),
		Create("return false;", new[] { 1, 2, 3 })
	];
}

[InheritsTests]
public class LinqCountEqualsZeroReversedToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return 0 == x.Count();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return !x.Any();"),
		Create("return true;", Enumerable.Empty<int>()),
		Create("return false;", new[] { 1, 2, 3 })
	];
}

[InheritsTests]
public class LinqCountEqualsZeroWithPredicateToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count(v => v > 5) == 0;
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
public class LinqCountEqualsZeroNotOptimizedTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() == 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return false;", Enumerable.Empty<int>()),
		Create("return true;", new[] { 42 }),
		Create("return false;", new[] { 1, 2 })
	];
}