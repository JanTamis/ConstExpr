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