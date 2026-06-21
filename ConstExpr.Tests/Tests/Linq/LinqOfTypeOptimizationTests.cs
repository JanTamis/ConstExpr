using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for OfType() optimization - verify duplicate OfType removal
/// </summary>
[InheritsTests]
public class LinqOfTypeOptimizationTests() : BaseTest<Func<object[], int>>(FastMathFlags.AssociativeMath)
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
		Create(_ => 6, [ new[] { 1, 2, 3 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ])
	];
}