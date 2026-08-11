using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Count() &lt;= 0 → !(source.Any()) and Count(predicate) &lt;= 0 → !(source.Any(predicate)).
/// </summary>
[InheritsTests]
public class LinqCountLessThanOrEqualZeroToAnyTests() : BaseTestWithRandomValues<Func<IEnumerable<int>, bool>>(FastMathFlags.Strict, LinqOptimizationMode.None)
{
	public override string TestMethod => GetString(x =>
	{
		return x.Count() <= 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => !x.Any()),
	];
}