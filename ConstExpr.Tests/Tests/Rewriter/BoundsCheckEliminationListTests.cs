using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A <c>List&lt;T&gt;</c> is first viewed as a span over its backing array. Safe here because the
///   list is only indexed and read — see the negative test below for what invalidates that.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationListTests() : BaseTest<Func<List<int>, int, int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString((values, i) =>
	{
		values[i] = i;

		return values[i] + values[0] + values.Count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((values, i) =>
		{
			ref var valuesRef = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(values));

			Unsafe.Add(ref valuesRef, (nuint) i) = i;

			return Unsafe.Add(ref valuesRef, (nuint) i) + valuesRef + values.Count;
		}, [ Unknown, Unknown ])
	];
}