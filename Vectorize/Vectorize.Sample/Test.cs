using ConstantExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vectorize.Sample;

[ConstExpr]
public static class Test
{
	public static float Sum(params IEnumerable<float> data)
	{
		return data
			// .Where(w => w % 0 == 0)
			.Sum();
	}

	// public static float Average(params IReadOnlyList<float> data)
	// {
	// 	return data.Average();
	// }
	//
	// public static float StdDev(params IReadOnlyList<float> data)
	// {
	// 	var sum = 0f;
	// 	var sumOfSquares = 0f;
	//
	// 	foreach (var item in data)
	// 	{
	// 		sum += item;
	// 		sumOfSquares += item * item;
	// 	}
	//
	// 	var mean = sum / data.Count;
	// 	var variance = sumOfSquares / data.Count - mean * mean;
	//
	// 	return MathF.Sqrt(variance);
	// }
	//
	// public static int StringLength(string value, Encoding encoding)
	// {
	// 	return encoding.GetByteCount(value);
	// }
	//
	// public static ReadOnlySpan<byte> StringBytes(string value, Encoding encoding)
	// {
	// 	return encoding.GetBytes(value);
	// }
	//
	// public static string Base64Encode(string value)
	// {
	// 	return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
	// }
	//
	// public async static Task<string> Waiting()
	// {
	// 	await Task.Delay(5000);
	// 	
	// 	return nameof(Test);
	// }
}