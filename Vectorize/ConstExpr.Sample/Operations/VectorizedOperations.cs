using System;
using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.SourceGenerator.Sample.Operations;

/// <summary>
///   Demonstrates auto-vectorization (<see cref="OptimizationFlags.AutoVectorization" />).
///   Each method below is a plain scalar loop over an array; when the generator intercepts a call with
///   a non-constant array it rewrites the loop into a SIMD <c>System.Numerics.Vector&lt;T&gt;</c> helper
///   (guarded by <c>Vector.IsHardwareAccelerated</c>, with a scalar tail for the remainder).
///   Build this project and inspect <c>Vectorize/ConstExpr.Sample/Generated/</c> to see the emitted code.
/// </summary>
[ConstExpr(
	MathOptimizations = FastMathFlags.All,
	Optimizations = OptimizationFlags.AutoVectorization)]
public static class VectorizedOperations
{
	/// <summary>
	///   Sum reduction over an <c>int</c> array → <c>Vector&lt;int&gt;</c> accumulator + <c>Vector.Sum</c>.
	/// </summary>
	public static int Sum(int[] values)
	{
		var sum = 0;

		foreach (var value in values)
		{
			sum += value;
		}

		return sum;
	}

	/// <summary>
	///   Sum of squares → <c>Vector&lt;int&gt;</c> with a per-lane multiply.
	/// </summary>
	public static int SumOfSquares(int[] values)
	{
		var sum = 0;

		for (var i = 0; i < values.Length; i++)
		{
			sum += values[i] * values[i];
		}

		return sum;
	}

	/// <summary>
	///   Dot product of two <c>int</c> arrays → element-wise <c>Vector&lt;int&gt;</c> multiply + <c>Vector.Sum</c>.
	/// </summary>
	public static int Dot(int[] left, int[] right)
	{
		var sum = 0;

		for (var i = 0; i < left.Length; i++)
		{
			sum += left[i] * right[i];
		}

		return sum;
	}

	/// <summary>
	///   Maximum of a <c>double</c> array. Vectorized only because <see cref="FastMathFlags.AssociativeMath" />
	///   is enabled (SIMD reorders the reduction).
	/// </summary>
	public static double Max(double[] values)
	{
		var max = Double.MinValue;

		foreach (var value in values)
		{
			max = Math.Max(max, value);
		}

		return max;
	}

	/// <summary>
	///   Element-wise map into a destination array → <c>Vector.LoadUnsafe</c> + <c>Vector.StoreUnsafe</c>.
	/// </summary>
	public static void Scale(int[] source, int[] destination)
	{
		for (var i = 0; i < source.Length; i++)
		{
			destination[i] = source[i] * 3 + 1;
		}
	}
}
