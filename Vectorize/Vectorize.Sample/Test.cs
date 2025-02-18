using System;
using System.Collections.Generic;
using System.Text;
using ConstantExpression;

namespace Vectorize.Sample;

public static class Test
{
	[ConstExpr]
	public static float Sum(IEnumerable<float> data)
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
		return Sum(data) / data.Count;
	}

	[ConstExpr]
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
	
	[ConstExpr]
	public static int StringLength(string value, Encoding encoding)
	{
		return encoding.GetByteCount(value);
	}
	
	[ConstExpr]
	public static string Base64Encode(string value)
	{
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
	}
}