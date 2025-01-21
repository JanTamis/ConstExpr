using System;

namespace Vectorize.Sample;

public class Test
{
	[Vectorize]
	public float TestMethod(ReadOnlySpan<float> data)
	{
		var sum = 0f;
		
		foreach (var item in data)
		{
			sum += item == 0f ? 0f : item;
		}
		
		return sum;
	}
}