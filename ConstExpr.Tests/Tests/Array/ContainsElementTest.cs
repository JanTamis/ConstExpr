using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ContainsElementTest() : BaseTest<Func<int[], int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((arr, value) =>
	{
		foreach (var item in arr)
		{
			if (item == value)
			{
				return true;
			}
		}

		return false;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return true;", new[] { 1, 2, 3, 4, 5 }, 3),
		Create("return false;", new[] { 10, 20, 30 }, 5)
	];
}