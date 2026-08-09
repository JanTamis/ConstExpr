using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

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
		CreateFolded(Enumerable.Empty<int>()),
		CreateFolded(new[] { 42 }),
		CreateFolded(new[] { 1, 2 })
	];
}