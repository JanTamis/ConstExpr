using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using ConstantExpression;

namespace Vectorize.Sample;

public static class Test
{
	[ConstExpr]
	public static float Sum(IReadOnlyList<float> data)
	{
		var sum = 0f;

		foreach (var item in data)
		{
			sum += item;
		}

		return sum;
	}

	[ConstExpr]
	public static float Average(IReadOnlyList<float> data)
	{
		var sum = 0f;

		foreach (var item in data)
		{
			sum += item;
		}

		return sum / data.Count;
	}

	// [ConstExpr]
	public static float StdDev(IReadOnlyList<float> data)
	{
		var sum = 0f;
		var sumOfSquares = 0f;

		foreach (var item in data)
		{
			sum += item;
			sumOfSquares += item * item;
		}

		var mean = sum / data.Count;
		var variance = sumOfSquares / data.Count - mean * mean;

		return MathF.Sqrt(variance);
	}
}