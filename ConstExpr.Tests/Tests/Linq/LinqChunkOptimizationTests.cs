using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Chunk(1) optimization - verify that Chunk(1) is converted to Select
/// Note: Chunk is only available in .NET 6+ so these tests are commented out for compatibility
/// </summary>
[InheritsTests]
public class LinqChunkOptimizationTests() : BaseTest<Func<int[], int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Length;
			var b = (x.Length + 2) / 3;
			var c = (x.Length + 1) / 2;
			var d = x[..5];
			var e = x[^4..];
			
			return a + b + c + d.Length + e.Length;
			""", Unknown),
		Create("return 9;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 10;", new[] { 1, 2, 3, 4, 5, 6 }),
		Create("throw new InvalidOperationException(\"Sequence contains no elements\");", new int[] { }),
	];
}