using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

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