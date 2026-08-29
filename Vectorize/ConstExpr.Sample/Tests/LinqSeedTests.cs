using System;
using System.Linq;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests;

/// <summary>
///   Runs each sequence-seeded LINQ helper from <see cref="LinqSeedOperations" /> against a plain
///   System.Linq reference over the same input and prints PASS/FAIL. The inputs are built from
///   runtime <c>var</c> values so the source stays opaque and the generator emits the unrolled helper.
/// </summary>
internal static class LinqSeedTests
{
	public static void RunTests(int a, int b, int c)
	{
		Console.WriteLine("\n??? LINQ SEED SEMANTICS ???\n");

		// a,b,c are runtime values (e.g. 10, 5, 20); mix in negatives explicitly.
		var mixed = new[] { a, -b, -c, b, -a, c }; // {10,-5,-20,5,-10,20}
		var allPositive = new[] { a, b, c }; // {10,5,20}
		var twoNeg = new[] { -b, -c }; // {-5,-20}

		Report("MaxOfNegatives(mixed)", LinqSeedOperations.MaxOfNegatives(mixed),
			mixed.Where(v => v < 0).Max());

		Report("MaxByIdentityOfNegatives(mixed)", LinqSeedOperations.MaxByIdentityOfNegatives(mixed),
			mixed.Where(v => v < 0).MaxBy(v => v));

		Report("MinOfNegatives(mixed)", LinqSeedOperations.MinOfNegatives(mixed),
			mixed.Where(v => v < 0).Min());

		Report("FarthestFromZeroNegative(mixed)", LinqSeedOperations.FarthestFromZeroNegative(mixed),
			mixed.Where(v => v < 0).MaxBy(v => v * v));

		Report("ProductOfNegatives(twoNeg)", LinqSeedOperations.ProductOfNegatives(twoNeg),
			twoNeg.Where(v => v < 0).Aggregate((acc, v) => acc * v));

		Report("MaxOfNegativesEnumerable(mixed)", LinqSeedOperations.MaxOfNegativesEnumerable(mixed),
			mixed.Where(v => v < 0).Max());

		Report("FarthestFromZeroNegativeEnumerable(mixed)", LinqSeedOperations.FarthestFromZeroNegativeEnumerable(mixed),
			mixed.Where(v => v < 0).MaxBy(v => v * v));

		Report("SumOfSquares(mixed)", LinqSeedOperations.SumOfSquares(mixed),
			mixed.Aggregate(0, (acc, v) => acc + v * v));

		Report("SumOfSquaresPlusOne(mixed)", LinqSeedOperations.SumOfSquaresPlusOne(mixed),
			mixed.Aggregate(0, (acc, v) => acc + v * v, acc => acc + 1));

		Report("SumOfNegativeSquares(mixed)", LinqSeedOperations.SumOfNegativeSquares(mixed),
			mixed.Where(v => v < 0).Aggregate(0, (acc, v) => acc + v * v));

		// Empty-after-filter: the helper must throw InvalidOperationException like System.Linq.
		try
		{
			var r = LinqSeedOperations.MaxOfNegatives(allPositive);
			Console.WriteLine($"[FAIL] MaxOfNegatives(allPositive): returned {r}, expected InvalidOperationException");
		}
		catch (InvalidOperationException)
		{
			Console.WriteLine("[PASS] MaxOfNegatives(allPositive) threw InvalidOperationException");
		}

		try
		{
			var r = LinqSeedOperations.FarthestFromZeroNegative(allPositive);
			Console.WriteLine($"[FAIL] FarthestFromZeroNegative(allPositive): returned {r}, expected InvalidOperationException");
		}
		catch (InvalidOperationException)
		{
			Console.WriteLine("[PASS] FarthestFromZeroNegative(allPositive) threw InvalidOperationException");
		}
	}

	private static void Report(string label, int actual, int expected)
	{
		Console.WriteLine(actual == expected
			? $"[PASS] {label} = {actual}"
			: $"[FAIL] {label}: got {actual}, expected {expected}");
	}
}