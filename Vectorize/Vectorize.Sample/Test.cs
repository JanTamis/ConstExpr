using System;

namespace Vectorize.Sample;

public class Test
{
	[Optimize, Vectorize]
	public float TestMethod(ReadOnlySpan<float> data)
	{
		var sum = 0f;
		
		foreach (var item in data)
		{
			if (false)
			{
				sum += MathF.Sqrt(item);
			}
		}
		
		return sum;
	}
}