using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A local array gets its own <c>ref</c> local, inserted directly after the declaration rather
///   than at the top of the body. A compound index is parenthesised so the <c>nuint</c> cast does
///   not bind tighter than the arithmetic.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationLocalArrayTests() : BaseTest<Func<int, int>>(optimizations: OptimizationFlags.BoundsCheckElimination)
{
	public override string TestMethod => GetString(n =>
	{
		var buf = new int[4];

		buf[n % 4] = n;

		return buf[n % 4] + buf[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var buf = new int[4];
			ref var bufRef = ref MemoryMarshal.GetArrayDataReference(buf);

			Unsafe.Add(ref bufRef, (nuint) (n % 4)) = n;

			return Unsafe.Add(ref bufRef, (nuint) (n % 4)) + bufRef;
		}, [ Unknown ]),

		Create(_ => 3, [ 3 ])
	];
}