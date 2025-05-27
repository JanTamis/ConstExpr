using ConstantExpression;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(Level = GenerationLevel.Performance)]
public static class Test
{
	static ReadOnlySpan<int> IReadOnlyList_21149831_Data => [ 0, 1, 1, 1, 2, 3, 4, 4, 4, 4 ];

	public static IEnumerator<int> GetEnumerator()
	{
		for (var i = 0; i < IReadOnlyList_21149831_Data.Length; i++)
		{
			yield return IReadOnlyList_21149831_Data[i];
		}
	}
	
	public static IEnumerable<float> IsOdd(params IEnumerable<float> data)
	{
		return data.Where(w => w % 2 != 0);
	}

	public static float Average(params IReadOnlyList<float> data)
	{
		return IsOdd(data).Average();
	}

	public static float StdDev(params IReadOnlyList<float> data)
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

	public static int StringLength(string value, Encoding encoding)
	{
		return encoding.GetByteCount(value);
	}

	public static ReadOnlySpan<byte> StringBytes(string value, Encoding encoding)
	{
		return encoding.GetBytes(value);
	}

	public static ICharCollection Base64Encode(string value)
	{
		return (ICharCollection) (object)Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
	}

	public async static Task<string> Waiting()
	{
		// await Task.Delay(1000);

		return nameof(Test);
	}

	public static ICustomCollection<int> Range(int count)
	{
		var random = new Random();
		var result = new List<int>(count);

		for (var i = 0; i < count; i++)
		{
			result.Add(random.Next(5));
		}

		return result.OrderBy(o => o) as ICustomCollection<int>;
	}

	public static ICustomCollection<string> Split(string value, char separator)
	{
		return (ICustomCollection<string>)(object)value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	public static ICustomCollection<int> Fibonacci(int count)
	{
		var items = new List<int>(count);
		
		int a = 0, b = 1;

		for (var i = 0; i < count; i++)
		{
			items.Add(a);

			var temp = a;
			a = b;
			b = temp + b;
		}
		
		return items as ICustomCollection<int>;
	}

	public static (float h, float s, float l) RgbToHsl(byte r, byte g, byte b)
	{
		var rn = r / 255.0f;
		var gn = g / 255.0f;
		var bn = b / 255.0f;

		var max = Math.Max(rn, Math.Max(gn, bn));
		var min = Math.Min(rn, Math.Min(gn, bn));
		var delta = max - min;

		var h = 0f;

		if (delta != 0)
		{
			if (max == rn)
			{
				h = (gn - bn) / delta % 6;
			}
			else if (max == gn)
			{
				h = (bn - rn) / delta + 2;
			}
			else
			{
				h = (rn - gn) / delta + 4;
			}

			h *= 60;

			if (h < 0)
			{
				h += 360;
			}
		}

		var l = (max + min) / 2;
		var s = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * l - 1));

		return (h, s, l);
	}
}