using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Count() > 0 → source.Any() and Count(predicate) > 0 → source.Any(predicate).
/// </summary>
[InheritsTests]
public class LinqCountGreaterThanZeroToAnyTests() : BaseTest<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimizationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() > 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Any()),
		Create(_ => false, [ Enumerable.Empty<int>() ]),
		Create(_ => true, [ new[] { 1, 2, 3 } ])
	];
}