using System;

namespace Vectorize.Sample;

public static class Test
{
	[ConstExpr]
	public static float Sum(ReadOnlySpan<float> data)
	{
		var sum = 0f;
		
		foreach (var item in data)
		{
			sum += item;
		}

		return sum;
	}
	
	[ConstExpr]
	public static float Average(ReadOnlySpan<float> data)
	{
		var sum = 0f;
		
		foreach (var item in data)
		{
			sum += item;
		}

		return sum / data.Length;
	}
}