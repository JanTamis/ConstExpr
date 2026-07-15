using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Count() >= 1 → source.Any() and Count(predicate) >= 1 → source.Any(predicate).
/// </summary>
[InheritsTests]
public class LinqCountGreaterOrEqualOneToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimizationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() >= 1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Any()),
		Create(_ => false, [ Enumerable.Empty<int>() ]),
		Create(_ => true, [ new[] { 1 } ]),
		Create(_ => true, [ new[] { 1, 2, 3 } ])
	];
}