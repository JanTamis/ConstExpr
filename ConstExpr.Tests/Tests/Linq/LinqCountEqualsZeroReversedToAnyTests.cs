using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

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
		CreateFolded(Enumerable.Empty<int>()),
		CreateFolded(new[] { 1, 2, 3 })
	];
}