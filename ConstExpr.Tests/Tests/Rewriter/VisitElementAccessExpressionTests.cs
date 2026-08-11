using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitElementAccessExpression - array/indexer constant evaluation
/// </summary>
[InheritsTests]
public class VisitElementAccessExpressionTests : BaseTestWithRandomValues<Func<int[], int, int, (int, int, int, int)>>
{
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