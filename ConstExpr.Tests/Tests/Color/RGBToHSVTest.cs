namespace ConstExpr.Tests.Color;

[InheritsTests]
public class RGBToHSVTest : BaseTestWithRandomValues<Func<byte, byte, byte, (double, double, double)>>
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
			double min = Byte.Min(Byte.Min(r, g), b);
			double v = Byte.Max(Byte.Max(r, g), b);
			var delta = v - min;
			var s = v == 0D ? 0D : delta / v;

			if (s != 0D)
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
					h += 360D;
				}
			}

			return (h, s, v * 0.00392156862745098);
		}),
		Create((_, g, b) =>
		{
			var h = 0D;
			double min = Byte.Min(g, b);
			var delta = 255D - min;
			var s = delta * 0.00392156862745098;

			if (s != 0D)
			{
				h = (g - b) / delta;
				h *= 60D;

				if (h < 0D)
				{
					h += 360D;
				}
			}

			return (h, s, 1D);
		}, [ (byte) 255, Unknown, Unknown ]),
		Create((r, g, _) =>
		{
			var h = 0D;
			double v = Byte.Max(r, g);
			var condVal = v == 0D ? 0D : 1D;

			if (condVal != 0D)
			{
				if (r == v)
				{
					h = g / v;
				}
				else if (g == v)
				{
					h = 2D - r / v;
				}
				else if (v == 0)
				{
					h = (r - g) / v + 4D;
				}

				h *= 60D;

				if (h < 0D)
					h += 360D;
			}

			return (h, condVal, v * 3.92156862745098E-3);
		}, [ Unknown, Unknown, (byte) 0 ]),
		Create((_, _, b) =>
		{
			var h = 0D;
			var condVal = b == 0D ? 0D : 1D;

			if (condVal != 0D)
			{
				h = b == 0 ? -60D : 240D;

				if (h < 0D)
					h += 360D;
			}

			return (h, condVal, b * 3.92156862745098E-3);
		}, [ (byte) 0, (byte) 0, Unknown ])
	];
}