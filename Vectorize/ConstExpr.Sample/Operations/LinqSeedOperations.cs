using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.SourceGenerator.Sample.Operations;

/// <summary>
///   Exercises the sequence-seeded LINQ unrollers (Max/Min/MaxBy/MinBy/Aggregate) on a genuinely
///   opaque runtime source, so the generator emits a real unrolled helper method rather than
///   constant-folding the whole chain. Each method has a plain-LINQ twin in
///   <see cref="Tests.LinqSeedTests" /> the sample compares against at runtime.
/// </summary>
[ConstExpr(
	MathOptimizations = FastMathFlags.All,
	Optimizations = OptimizationFlags.All | OptimizationFlags.BoundsCheckElimination,
	LinqOptimization = LinqOptimizationMode.Unroll)]
public static class LinqSeedOperations
{
	/// <summary>Filter then Max — the seed must come from the first element that passes the filter.</summary>
	public static int MaxOfNegatives(params int[] numbers)
	{
		return numbers.Where(v => v < 0).Max();
	}

	/// <summary>Filter then Min.</summary>
	public static int MinOfNegatives(params int[] numbers)
	{
		return numbers.Where(v => v < 0).Min();
	}

	/// <summary>Filter then MaxBy on a derived (non-identity) key — most negative wins on largest square.</summary>
	public static int FarthestFromZeroNegative(params int[] numbers)
	{
		return numbers.Where(v => v < 0).MaxBy(v => v * v);
	}

	/// <summary>Filter then seedless Aggregate (product) — seed is the first passing element.</summary>
	public static int ProductOfNegatives(params int[] numbers)
	{
		return numbers.Where(v => v < 0).Aggregate((acc, v) => acc * v);
	}

	/// <summary>
	///   Same as <see cref="MaxOfNegatives" /> but over a plain IEnumerable — exercises the
	///   enumerator (non-array) first-flag path.
	/// </summary>
	public static int MaxOfNegativesEnumerable(IEnumerable<int> numbers)
	{
		return numbers.Where(v => v < 0).Max();
	}

	/// <summary>MaxBy over a plain IEnumerable — enumerator first-flag path with a key local.</summary>
	public static int FarthestFromZeroNegativeEnumerable(IEnumerable<int> numbers)
	{
		return numbers.Where(v => v < 0).MaxBy(v => v * v);
	}

	/// <summary>Explicitly seeded Aggregate — seed literally from the seed argument, fold every element.</summary>
	public static int SumOfSquares(params int[] numbers)
	{
		return numbers.Aggregate(0, (acc, v) => acc + v * v);
	}

	/// <summary>Seeded Aggregate with a result selector (3-arg overload).</summary>
	public static int SumOfSquaresPlusOne(params int[] numbers)
	{
		return numbers.Aggregate(0, (acc, v) => acc + v * v, acc => acc + 1);
	}

	/// <summary>Filter then seeded Aggregate — filter and explicit seed both honoured.</summary>
	public static int SumOfNegativeSquares(params int[] numbers)
	{
		return numbers.Where(v => v < 0).Aggregate(0, (acc, v) => acc + v * v);
	}
}