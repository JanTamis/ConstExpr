using System.Numerics.Tensors;
using System.Runtime.InteropServices;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Reverse() optimization:
///   - Reverse().Reverse() => original collection
///   - Order().Reverse() => OrderDescending()
///   - OrderBy(k).Reverse() => OrderByDescending(k)
///   - OrderDescending().Reverse() => Order()
///   - OrderByDescending(k).Reverse() => OrderBy(k)
/// </summary>
[InheritsTests]
public class LinqReverseOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Reverse().Reverse() => original
		var a = x.Reverse().Reverse().First();

		// Order().Reverse() => OrderDescending()
		var b = x.Order().Reverse().First();

		// OrderBy(v => v).Reverse() => OrderByDescending(v => v)
		var c = x.OrderBy(v => v).Reverse().First();

		// OrderDescending().Reverse() => Order()
		var d = x.OrderDescending().Reverse().First();

		// OrderByDescending(v => v).Reverse() => OrderBy(v => v)
		var e = x.OrderByDescending(v => v).Reverse().First();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			ref var xRef = ref MemoryMarshal.GetArrayDataReference(x);

			return (TensorPrimitives.Max(x) << 1) + (TensorPrimitives.Min(x) << 1) + xRef;
		}),
	];
}