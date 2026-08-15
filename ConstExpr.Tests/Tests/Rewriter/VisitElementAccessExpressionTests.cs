using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitElementAccessExpression - array/indexer constant evaluation
/// </summary>
[InheritsTests]
public class VisitElementAccessExpressionTests : BaseTestWithRandomValues<Func<int[], int, int, (int, int, int, int)>>
{
	// arr[2] needs at least three elements, and index1/index2 both have to be in range. Full-range random
	// ints never were, so every draw threw and was discarded - the random pass checked nothing at all. Capped
	// to 0-3 against the generator's 0-8 element arrays.
	protected override int MaxRandomMagnitudeBits => 2;

	// Three constraints have to hold at once, so most draws still throw. A throwing draw costs only a
	// delegate invocation (no rewrite), so drawing more is the cheap way to get real coverage here: 50 draws
	// yield 9 checked cases, 10 draws yielded 1.
	protected override int RandomTestCaseCount => 50;

	// Floor well under the count actually achieved, so a future generator or seed change that silently
	// starves this class again fails loudly instead of quietly checking one case.
	protected override int MinRandomTestCaseCount => 2;

	public override string TestMethod => GetString((arr, index1, index2) =>
	{
		var a = arr[0];
		var b = arr[2];
		var c = arr[index1];
		var d = arr[index2];

		return (a, b, c, d);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((arr, index1, index2) =>
		{
			ref var arrRef = ref MemoryMarshal.GetArrayDataReference(arr);

			return (arrRef, Unsafe.Add(ref arrRef, 2), Unsafe.Add(ref arrRef, index1), Unsafe.Add(ref arrRef, index2));
		}),
	];
}