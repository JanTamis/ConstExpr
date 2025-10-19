using ConstExpr.Core.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class Test
{
	public static IEnumerable<double> IsOdd(params IEnumerable<double> data)
	{
		return data.Where(w => w % 2 != 0);
	}

	public static int[] GetArray(params int[] items)
	{
		return items[1..5];
	}

	public static double Average(params IReadOnlyList<double> data)
	{
		return data.Average();
	}

	public static bool IsPrime(int number)
	{
		switch (number)
		{
			case < 2:
				return false;
			case 2:
				return true;
		}

		if (number % 2 == 0)
		{
			return false;
		}

		var sqrt = (int)Math.Sqrt(number);

		for (var i = 3; i <= sqrt; i += 2)
		{
			if (number % i == 0)
			{
				return false;
			}
		}

		return true;
	}

	public static double StdDev(params IReadOnlyList<double> data)
	{
		var sum = 0d;
		var sumOfSquares = 0d;

		foreach (var item in data)
		{
			sum += item;
			sumOfSquares += item * item;
		}

		var mean = sum / data.Count;
		var variance = sumOfSquares / data.Count - mean * mean;

		return Math.Sqrt(variance);
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
		return (ICharCollection)(object)Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
	}

	public async static Task<string> Waiting()
	{
		// await Task.Delay(1000);

		return nameof(Test);
	}

	public static IList<byte> Range(int count)
	{
		var random = new Random();
		var result = new List<byte>(count);

		for (var i = 0; i < count; i++)
		{
			result.Add((byte)random.Next(5));
		}

		return result; // result.OrderBy(o => o).ToList();
	}

	public static IReadOnlyList<string> Split(string value, char separator)
	{
		return value.Split([separator], StringSplitOptions.TrimEntries);
	}

	public static string ToString<T>(this T value) where T : Enum
	{
		return value.ToString();
	}

	public static IEnumerable<string> GetNames<T>() where T : struct, Enum
	{
		return Enum.GetNames<T>();
	}

	public static IEnumerable<long> FibonacciSequence(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);

		var a = 0L;
		var b = 1L;

		for (var i = 0; i < count; i++)
		{
			yield return a;

			checked
			{
				var next = a + b;
				a = b;
				b = next;
			}
		}
	}

	public static IEnumerable<int> PrimesUpTo(int max)
	{
		if (max < 2)
		{
			yield break;
		}

		// Simple sieve for reasonable max values
		var sieve = new BitArray(max + 1);

		for (var p = 2; p * p <= max; p++)
		{
			if (!sieve[p])
			{
				for (var m = p * p; m <= max; m += p)
				{
					sieve[m] = true;
				}
			}
		}

		for (var i = 2; i <= max; i++)
		{
			if (!sieve[i])
			{
				yield return i;
			}
		}
	}

	public static int Clamp(int value, int min, int max)
	{
		if (min > max)
		{
			throw new ArgumentException("min cannot be greater than max");
		}

		if (value < min)
		{
			return min;
		}

		if (value > max)
		{
			return max;
		}

		return value;
	}

	public static double Map(double value, double inMin, double inMax, double outMin, double outMax)
	{
		if (Math.Abs(inMax - inMin) < double.Epsilon)
		{
			throw new ArgumentException("Input range cannot be zero", nameof(inMax));
		}

		var t = (value - inMin) / (inMax - inMin);
		return outMin + t * (outMax - outMin);
	}

	public static (byte r, byte g, byte b) HslToRgb(float h, float s, float l)
	{
		h %= 360f;

		if (h < 0)
		{
			h += 360f;
		}

		s = Clamp01(s);
		l = Clamp01(l);

		if (s == 0f)
		{
			var vGray = (byte)MathF.Round(l * 255f);
			return (vGray, vGray, vGray);
		}

		var c = (1 - MathF.Abs(2 * l - 1)) * s;
		var hp = h / 60f;
		var x = c * (1 - MathF.Abs(hp % 2 - 1));

		float r1, g1, b1;

		switch (hp)
		{
			case < 1:
				r1 = c;
				g1 = x;
				b1 = 0;
				break;
			case < 2:
				r1 = x;
				g1 = c;
				b1 = 0;
				break;
			case < 3:
				r1 = 0;
				g1 = c;
				b1 = x;
				break;
			case < 4:
				r1 = 0;
				g1 = x;
				b1 = c;
				break;
			case < 5:
				r1 = x;
				g1 = 0;
				b1 = c;
				break;
			default:
				r1 = c;
				g1 = 0;
				b1 = x; // 5-6
				break;
		}

		var m = l - c / 2f;
		var r = (byte)MathF.Round((r1 + m) * 255f);
		var g = (byte)MathF.Round((g1 + m) * 255f);
		var b = (byte)MathF.Round((b1 + m) * 255f);
		return (r, g, b);
	}

	public static float Luminance(byte r, byte g, byte b)
	{
		var rl = Channel(r);
		var gl = Channel(g);
		var bl = Channel(b);

		return 0.2126f * rl + 0.7152f * gl + 0.0722f * bl;

		static float Channel(byte c)
		{
			var cs = c / 255f;

			return cs <= 0.03928f
				? cs / 12.92f
				: MathF.Pow((cs + 0.055f) / 1.055f, 2.4f);
		}
	}

	public static float ContrastRatio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
	{
		var l1 = Luminance(r1, g1, b1);
		var l2 = Luminance(r2, g2, b2);

		if (l1 < l2)
		{
			(l1, l2) = (l2, l1);
		}

		return (l1 + 0.05f) / (l2 + 0.05f);
	}

	private static float Clamp01(float v)
	{
		return v switch
		{
			< 0f => 0f,
			> 1f => 1f,
			_ => v
		};
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

	public static (byte r, byte g, byte b) BlendRgb(byte rDst, byte gDst, byte bDst,
																									byte rSrc, byte gSrc, byte bSrc,
																									float alpha, bool gammaCorrect = true)
	{
		alpha = Clamp01(alpha);

		if (gammaCorrect)
		{
			static float ToLinear(byte c)
			{
				var cs = c / 255f;
				return cs <= 0.04045f ? cs / 12.92f : MathF.Pow((cs + 0.055f) / 1.055f, 2.4f);
			}

			static byte ToSrgbByte(float l)
			{
				if (l <= 0f) return 0;
				if (l >= 1f) return 255;
				var cs = l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1f / 2.4f) - 0.055f;
				return (byte)MathF.Round(MathF.Max(0f, MathF.Min(1f, cs)) * 255f);
			}

			var lrDst = ToLinear(rDst);
			var lgDst = ToLinear(gDst);
			var lbDst = ToLinear(bDst);

			var lrSrc = ToLinear(rSrc);
			var lgSrc = ToLinear(gSrc);
			var lbSrc = ToLinear(bSrc);

			var lr = alpha * lrSrc + (1f - alpha) * lrDst;
			var lg = alpha * lgSrc + (1f - alpha) * lgDst;
			var lb = alpha * lbSrc + (1f - alpha) * lbDst;

			return (ToSrgbByte(lr), ToSrgbByte(lg), ToSrgbByte(lb));
		}

		static byte LerpByte(byte a, byte b, float t)
		{
			var fa = a / 255f;
			var fb = b / 255f;
			var fr = t * fb + (1f - t) * fa;
			return (byte)MathF.Round(MathF.Max(0f, MathF.Min(1f, fr)) * 255f);
		}

		var r = LerpByte(rDst, rSrc, alpha);
		var g = LerpByte(gDst, gSrc, alpha);
		var b = LerpByte(bDst, bSrc, alpha);
		return (r, g, b);
	}

	public static string InterpolationTest(string name, int age, double height)
	{
		return $"Name: {name}, Age: {age}, Height: {height:N3} cm";
	}

	// Complex mathematical function with multiple parameters
	public static double PolynomialEvaluate(double x, double a, double b, double c, double d)
	{
		return a * x * x * x + b * x * x + c * x + d;
	}

	// String manipulation with multiple parameters
	public static string FormatFullName(string firstName, string middleName, string lastName, bool includeMiddle)
	{
		if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
		{
			throw new ArgumentException("First and last name are required");
		}

		if (includeMiddle && !string.IsNullOrWhiteSpace(middleName))
		{
			return $"{firstName} {middleName} {lastName}";
		}

		return $"{firstName} {lastName}";
	}

	// Geometric calculation with multiple parameters
	public static double TriangleArea(double sideA, double sideB, double angleC)
	{
		if (sideA <= 0 || sideB <= 0)
		{
			throw new ArgumentException("Sides must be positive");
		}

		var angleRad = angleC * Math.PI / 180.0;
		return 0.5 * sideA * sideB * Math.Sin(angleRad);
	}

	// Statistical function with multiple parameters
	public static double WeightedAverage(double value1, double weight1, double value2, double weight2, double value3, double weight3)
	{
		var totalWeight = weight1 + weight2 + weight3;

		if (Math.Abs(totalWeight) < double.Epsilon)
		{
			throw new ArgumentException("Total weight cannot be zero");
		}

		return (value1 * weight1 + value2 * weight2 + value3 * weight3) / totalWeight;
	}

	// Date calculation with multiple parameters
	public static int DaysBetweenDates(int year1, int month1, int day1, int year2, int month2, int day2)
	{
		var date1 = new DateTime(year1, month1, day1);
		var date2 = new DateTime(year2, month2, day2);
		return Math.Abs((date2 - date1).Days);
	}

	// Complex conditional logic with multiple parameters
	public static string DetermineGrade(double score, double maxScore, bool useCurve, double curveBonus)
	{
		if (maxScore <= 0)
		{
			throw new ArgumentException("Max score must be positive");
		}

		var percentage = (score / maxScore) * 100.0;

		if (useCurve)
		{
			percentage += curveBonus;
		}

		percentage = Math.Min(percentage, 100.0);

		return percentage switch
		{
			>= 90.0 => "A",
			>= 80.0 => "B",
			>= 70.0 => "C",
			>= 60.0 => "D",
			_ => "F"
		};
	}

	// Physics calculation with multiple parameters
	public static double ProjectileMaxHeight(double initialVelocity, double launchAngle, double gravity)
	{
		if (gravity <= 0)
		{
			throw new ArgumentException("Gravity must be positive");
		}

		var angleRad = launchAngle * Math.PI / 180.0;
		var verticalVelocity = initialVelocity * Math.Sin(angleRad);
		return (verticalVelocity * verticalVelocity) / (2 * gravity);
	}

	// Financial calculation with multiple parameters
	public static double CompoundInterest(double principal, double rate, int timesCompounded, double years)
	{
		if (principal < 0 || rate < 0 || timesCompounded <= 0 || years < 0)
		{
			throw new ArgumentException("Invalid input parameters");
		}

		return principal * Math.Pow(1.0 + rate / timesCompounded, timesCompounded * years);
	}

	// Text processing with multiple parameters
	public static string GenerateSlug(string text, int maxLength, char separator, bool toLowerCase)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}

		var result = text.Trim();

		if (toLowerCase)
		{
			result = result.ToLowerInvariant();
		}

		result = result.Replace(' ', separator);
		result = new string(result.Where(c => char.IsLetterOrDigit(c) || c == separator).ToArray());

		if (result.Length > maxLength && maxLength > 0)
		{
			result = result.Substring(0, maxLength);
		}

		return result;
	}

	// Array manipulation with multiple parameters
	public static int[] FilterAndTransform(IEnumerable<int> array, int minValue, int maxValue, int multiplier)
	{
		return array
			.Where(x => x >= minValue && x <= maxValue)
			.Select(x => x * multiplier)
			.ToArray();
	}
}