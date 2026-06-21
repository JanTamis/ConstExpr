using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Color;

[InheritsTests]
public class RGBToHSVTest() : BaseTest<Func<byte, byte, byte, (double, double, double)>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((r, g, b) =>
	{
		double delta, min;
		double h = 0, s, v;

		min = System.Math.Min(System.Math.Min(r, g), b);
		v = System.Math.Max(System.Math.Max(r, g), b);
		delta = v - min;

		if (v == 0.0)
		{
			s = 0;
		}
		else
		{
			s = delta / v;
		}

		if (s == 0)
		{
			h = 0.0;
		}
		else
		{
			if (r == v)
			{
				h = (g - b) / delta;
			}
			else if (g == v)
			{
				h = 2 + (b - r) / delta;
			}
			else if (b == v)
			{
				h = 4 + (r - g) / delta;
			}

			h *= 60;

			if (h < 0.0)
			{
				h = h + 360;
			}
		}

		return (h, s, v / 255);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((r, g, b) =>
		{
			var h = 0D;
			var min = Byte.Min(Byte.Min(r, g), b);
			var v = Byte.Max(Byte.Max(r, g), b);
			var delta = v - min;
			var s = v == 0D ? 0D : delta / v;

			if (s == 0D)
			{
				h = 0D;
			}
			else
			{
				if (r == v)
				{
					h = (g - b) / delta;
				}
				else if (g == v)
				{
					h = (b - r) / delta + 2D;
				}
				else if (b == v)
				{
					h = (r - g) / delta + 4D;
				}

				h *= 60D;

				if (h < 0D)
				{
					h = h + 360D;
				}
			}

			return (h, s, v * 0.00392156862745098);
		})
	];
}