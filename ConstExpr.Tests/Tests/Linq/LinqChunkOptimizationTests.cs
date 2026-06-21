using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Chunk(1) optimization - verify that Chunk(1) is converted to Select
///   Note: Chunk is only available in .NET 6+ so these tests are commented out for compatibility
/// </summary>
[InheritsTests]
public class LinqChunkOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x =>
	{
		// Chunk(1) => x.Select(x => new[] { x })
		var a = x.Chunk(1).Count();

		// Chunk with non-constant size should NOT be optimized
		var size = 3;
		var b = x.Chunk(size).Count();

		// Chunk with size > 1 should NOT be optimized without follow-up operation
		var c = x.Chunk(2).ToArray().Length;

		var d = x.Chunk(5).First();

		var e = x.Chunk(4).Last();

		return a + b + c + d.Length + e.Length;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			var sum = x.Length + 2;

			return x.Length + ((int)(sum * 1431655766L >> 32) - (sum >> 31)) + (x.Length + 1) / 2 + x[..5].Length + x[^4..].Length;
		}),
		Create(_ => 16, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 18, [ new[] { 1, 2, 3, 4, 5, 6 } ]),
		Create(_ =>
		{
			throw new InvalidOperationException("Sequence contains no elements");
		}, [ System.Array.Empty<int>() ])
	];
}