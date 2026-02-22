namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for OfType() optimization - verify duplicate OfType removal
/// </summary>
[InheritsTests]
public class LinqOfTypeOptimizationTests : BaseTest<Func<object[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OfType<int>().OfType<int>() => OfType<int>()
		var a = x.OfType<int>().OfType<int>().Count();

		// Cast<int>().OfType<int>() => Cast<int>()
		var b = x.Cast<int>().OfType<int>().Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Length;
			var b = x.Length;
			
			return a + b;
			""", Unknown),
		Create("return 6;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

