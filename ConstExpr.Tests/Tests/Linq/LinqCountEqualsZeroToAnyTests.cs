using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Count() == 0 → !(source.Any()) and Count(predicate) == 0 → !(source.Any(predicate)).
///   Also covers the reversed form: 0 == Count().
///   Uses LinqOptimizationMode.None so Count() is not replaced by an unrolled helper before
///   the binary optimizer can match the x.Count() syntax.
/// </summary>
[InheritsTests]
public class LinqCountEqualsZeroToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimizationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => !x.Any()),
		Create(_ => true, [ Enumerable.Empty<int>() ]),
		Create(_ => false, [ new[] { 1, 2, 3 } ])
	];
}

[InheritsTests]
public class LinqCountEqualsZeroReversedToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimizationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return 0 == x.Count();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => !x.Any()),
		Create(_ => true, [ Enumerable.Empty<int>() ]),
		Create(_ => false, [ new[] { 1, 2, 3 } ])
	];
}

[InheritsTests]
public class LinqCountEqualsZeroWithPredicateToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimizationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count(v => v > 5) == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => !x.Any(v => v > 5)),
		Create(_ => true, [ Enumerable.Empty<int>() ]),
		Create(_ => true, [ new[] { 1, 2, 3 } ]),
		Create(_ => false, [ new[] { 1, 6, 3 } ])
	];
}

[InheritsTests]
public class LinqCountEqualsZeroNotOptimizedTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimizationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() == 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => false, [ Enumerable.Empty<int>() ]),
		Create(_ => true, [ new[] { 42 } ]),
		Create(_ => false, [ new[] { 1, 2 } ])
	];
}