namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   The merge requires every index <c>0..N-1</c> to be assigned exactly once, in ascending order.
///   When the indices are written out of order the body is left untouched, because reordering the
///   values into an initializer could change evaluation order.
/// </summary>
[InheritsTests]
public class ArrayElementInitializerOutOfOrderTest : BaseTest<Func<int[], int[]>>
{
	public override string TestMethod => GetString(numbers =>
	{
		var result = new int[3];

		result[0] = numbers[0];
		result[2] = numbers[2];
		result[1] = numbers[1];

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown)
	];
}