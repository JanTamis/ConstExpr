namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for OfType() optimization - verify duplicate OfType removal
/// </summary>
[InheritsTests]
public class LinqOfTypeOptimizationTests : BaseTestWithRandomValues<Func<object[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OfType<int>().OfType<int>() => OfType<int>()
		var a = x.OfType<int>().OfType<int>().Count();

		// Cast<int>().OfType<int>() => Cast<int>()
		var b = x.Cast<int>().OfType<int>().Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.Length << 1),
	];
}