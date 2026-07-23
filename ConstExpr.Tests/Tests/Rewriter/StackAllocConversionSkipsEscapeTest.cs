using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Guard test: an array that escapes the method — here returned to the caller — must stay a heap
///   array. A <c>Span&lt;T&gt;</c> backed by <c>stackalloc</c> cannot be returned (its storage dies
///   with the frame), so returning the local is not a span-safe use and the conversion is refused.
/// </summary>
[InheritsTests]
public class StackAllocConversionSkipsEscapeTest() : BaseTest<Func<int, int[]>>(optimizations: OptimizationFlags.StackAllocConversion)
{
	public override string TestMethod => GetString(n =>
	{
		var arr = new int[8];

		arr[n % 8]++;

		return arr;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// `arr` escapes via return, so it is left as a heap array, unchanged.
		Create(n =>
		{
			var arr = new int[8];

			arr[n % 8]++;

			return arr;
		}, [ Unknown ])
	];
}