using System;

namespace Vectorize.Sample;

public class Test
{
	[ConstExpr]
	public float TestMethod(ReadOnlySpan<float> data)
	{
		var sum = 0f;

		for (var i = 0; i < data.Length; i++)
		{
			sum += data[i];
		}
		
		return sum / data.Length;
	}
}