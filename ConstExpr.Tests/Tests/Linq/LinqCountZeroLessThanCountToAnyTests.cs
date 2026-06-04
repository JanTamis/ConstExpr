using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   0 &lt; Count() → source.Any() and 0 &lt; Count(predicate) → source.Any(predicate).
/// </summary>
[InheritsTests]
public class LinqCountZeroLessThanCountToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return 0 < x.Count();
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Any()),
		Create(_ => false, [ Enumerable.Empty<int>() ]),
		Create(_ => true, [ new[] { 1 } ]),
		Create(_ => true, [ new[] { 1, 2, 3 } ])
	];
}

[InheritsTests]
public class LinqCountZeroLessThanCountWithPredicateToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimisationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return 0 < x.Count(v => v > 5);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Any(v => v > 5)),
		Create(_ => false, [ Enumerable.Empty<int>() ]),
		Create(_ => false, [ new[] { 1, 2, 3 } ]),
		Create(_ => true, [ new[] { 6, 7 } ])
	];
}