using System;

namespace Vectorize.Sample;

public class Test
{
	[ConstExpr]
	public float TestMethod(ReadOnlySpan<float> data)
	{
		var sum = 0f;
		
		foreach (var item in data)
		{
			if (item > 0f)
			{
				sum += item;
			}
		}
		
		return sum;
	}
}