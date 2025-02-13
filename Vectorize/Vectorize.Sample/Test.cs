using System;
using System.Collections.Immutable;
using System.Data;
using ConstantExpression;

namespace Vectorize.Sample;

public static class Test
{
	[ConstExpr]
	public static float Sum(ImmutableArray<float> data)
	{
		var sum = 0f;

		foreach (var item in data)
		{
			sum += item;
		}

		return sum;
	}

	[ConstExpr]
	public static float Average(ImmutableArray<float> data)
	{
		var sum = 0f;

		foreach (var item in data)
		{
			sum += item;
		}

		return sum / data.Length;
	}

	[ConstExpr]
	public static float StdDev(ImmutableArray<float> data)
	{
		var sum = 0f;
		var sumOfSquares = 0f;

		foreach (var item in data)
		{
			sum += item;
			sumOfSquares += item * item;
		}

		var mean = sum / data.Length;
		var variance = sumOfSquares / data.Length - mean * mean;

		return MathF.Sqrt(variance);
	}
}