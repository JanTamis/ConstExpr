using System;
using ConstExpr.SourceGenerator.Sample.Operations;

namespace ConstExpr.SourceGenerator.Sample.Tests;

internal class VectorizedOperationsTests
{
	// The arrays are runtime values, so the generator emits the vectorized interceptors
	// (constant arrays would be folded away before the vectorization pass runs).
	public static void RunTests(int[] data, int[] other, double[] reals)
	{
		Console.WriteLine("=== VECTORIZED OPERATIONS ===\n");

		Console.WriteLine($"[VEC] Sum(data): {VectorizedOperations.Sum(data)}");
		Console.WriteLine($"[VEC] SumOfSquares(data): {VectorizedOperations.SumOfSquares(data)}");
		Console.WriteLine($"[VEC] Dot(data, other): {VectorizedOperations.Dot(data, other)}");
		Console.WriteLine($"[VEC] Max(reals): {VectorizedOperations.Max(reals):F2}");

		var destination = new int[data.Length];
		VectorizedOperations.Scale(data, destination);
		Console.WriteLine($"[VEC] Scale(data): [{String.Join(", ", destination)}]");
	}
}
